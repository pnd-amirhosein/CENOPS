// Note canvas interactions. One delegated controller per editor; no handlers on individual blocks.
// Geometry is measured from the visible card/table, never inferred from padded wrappers.
window.cenopsNoteDrag = (() => {
    const bindings = new WeakMap();
    const directions = ['note-drop-before', 'note-drop-after', 'note-drop-left', 'note-drop-right'];
    const tableMarks = ['note-table-col-intent', 'note-table-row-intent'];
    const sideMarks = ['note-side-right-intent', 'note-side-left-intent'];

    function attach(root, dotnet) {
        if (!root || bindings.has(root)) return;
        const spatial = root.dataset.noteSpatial === 'true';
        let dragging = null;
        let destination = null;
        let scheduled = false;
        const observed = new Set();
        const resize = typeof ResizeObserver === 'function'
            ? new ResizeObserver(() => scheduleMeasure()) : null;

        function clearClasses(selectors, classes) {
            root.querySelectorAll(selectors).forEach(el => el.classList.remove(...classes));
        }
        function cleanupDrag() {
            clearClasses('.note-dragging,' + directions.map(c => '.' + c).join(','),
                ['note-dragging', ...directions]);
            dragging = destination = null;
        }
        function scheduleMeasure() {
            if (scheduled) return;
            scheduled = true;
            requestAnimationFrame(() => {
                scheduled = false;
                if (!root.isConnected) return;
                for (const node of observed) {
                    if (!root.contains(node)) { resize?.unobserve(node); observed.delete(node); }
                }
                root.querySelectorAll('.note-table-frame').forEach(frame => {
                    const matrix = frame.querySelector('.note-table-matrix');
                    if (!matrix) return;
                    if (!observed.has(frame)) {
                        observed.add(frame);
                        resize?.observe(frame);
                    }
                    if (!observed.has(matrix)) {
                        observed.add(matrix);
                        resize?.observe(matrix);
                    }
                    const cells = Array.from(matrix.querySelectorAll('.note-table-cell'));
                    const firstRow = cells.filter(cell => cell.dataset.noteTableRow === '0');
                    const firstColumn = cells.filter(cell => cell.dataset.noteTableCol === '0');
                    if (!firstRow.length || !firstColumn.length) return;
                    const base = frame.getBoundingClientRect();
                    const table = matrix.getBoundingClientRect();
                    frame.style.setProperty('--table-top', `${table.top - base.top}px`);
                    frame.style.setProperty('--table-left', `${table.left - base.left}px`);
                    frame.style.setProperty('--table-right', `${base.right - table.right}px`);
                    frame.style.setProperty('--table-height', `${table.height}px`);
                    firstRow.forEach((cell, index) => {
                        const line = frame.querySelectorAll('.note-table-insert-col')[index];
                        if (line) line.style.right = `${base.right - cell.getBoundingClientRect().right}px`;
                    });
                    const lastCol = frame.querySelectorAll('.note-table-insert-col')[firstRow.length];
                    if (lastCol) lastCol.style.right = `${base.right - firstRow[firstRow.length - 1].getBoundingClientRect().left}px`;
                    firstColumn.forEach((cell, index) => {
                        const line = frame.querySelectorAll('.note-table-insert-row')[index];
                        if (line) line.style.top = `${cell.getBoundingClientRect().top - base.top}px`;
                    });
                    const lastRow = frame.querySelectorAll('.note-table-insert-row')[firstColumn.length];
                    if (lastRow) lastRow.style.top = `${firstColumn[firstColumn.length - 1].getBoundingClientRect().bottom - base.top}px`;
                    frame.querySelectorAll('.note-table-cell textarea').forEach(growTextarea);
                });
            });
        }
        function growTextarea(textarea) {
            if (!textarea || !textarea.isConnected) return;
            // Grow with content instead of creating the default two-line textarea scrollbar.
            textarea.style.height = 'auto';
            textarea.style.height = `${Math.max(77, textarea.scrollHeight)}px`;
        }
        function pointInRect(x, y, rect, margin = 0) {
            return x >= rect.left - margin && x <= rect.right + margin &&
                y >= rect.top - margin && y <= rect.bottom + margin;
        }
        function visibleCard(block) {
            return block.querySelector(':scope > .note-edit-block') || block;
        }
        function dropSide(block, x, y) {
            const rect = visibleCard(block).getBoundingClientRect();
            const vertical = (y - rect.top) / Math.max(rect.height, 1);
            if (!spatial) return vertical < .5 ? 'above' : 'below';
            // Top/bottom bands are real targets; the remaining central *halves* are
            // left/right targets, even when dropping far from a card's outer edge.
            if (vertical <= .19) return 'above';
            if (vertical >= .81) return 'below';
            return x < rect.left + rect.width / 2 ? 'left' : 'right';
        }
        function hoveredBlock(e) {
            const direct = e.target.closest?.('[data-note-block]');
            if (direct && root.contains(direct)) return direct;
            // Support gaps between adjacent blocks as practical drop targets.
            if (!dragging) return null;
            const candidates = [...root.querySelectorAll('[data-note-block]')];
            let best = null, distance = Infinity;
            for (const candidate of candidates) {
                const rect = visibleCard(candidate).getBoundingClientRect();
                const dx = Math.max(rect.left - e.clientX, 0, e.clientX - rect.right);
                const dy = Math.max(rect.top - e.clientY, 0, e.clientY - rect.bottom);
                const d = Math.hypot(dx, dy);
                if (d < distance && d <= 34) { best = candidate; distance = d; }
            }
            return best;
        }
        function onStart(e) {
            const handle = e.target.closest?.('[data-note-drag]');
            if (!handle || !root.contains(handle) || !e.dataTransfer) return;
            dragging = handle.dataset.noteDrag;
            e.dataTransfer.setData('text/plain', dragging);
            e.dataTransfer.effectAllowed = 'move';
            handle.closest('[data-note-block]')?.classList.add('note-dragging');
            clearClasses('.note-side-right-intent,.note-side-left-intent', sideMarks);
        }
        function onOver(e) {
            if (!dragging) return;
            const block = hoveredBlock(e);
            if (!block) return;
            e.preventDefault();
            if (e.dataTransfer) e.dataTransfer.dropEffect = 'move';
            clearClasses(directions.map(c => '.' + c).join(','), directions);
            const side = dropSide(block, e.clientX, e.clientY);
            block.classList.add({
                above: 'note-drop-before', below: 'note-drop-after',
                left: 'note-drop-left', right: 'note-drop-right'
            }[side]);
            destination = { key: block.dataset.noteBlock, side };
        }
        function onDrop(e) {
            if (!dragging) return;
            e.preventDefault();
            const block = hoveredBlock(e);
            const target = block?.dataset.noteBlock ?? destination?.key;
            const side = block ? dropSide(block, e.clientX, e.clientY) : destination?.side;
            const source = dragging;
            cleanupDrag();
            if (!target || target === source || !side) return;
            const action = spatial ? 'MoveBlockSpatialAsync' : 'MoveBlockAsync';
            const args = spatial ? [source, target, side] : [source, target, side === 'below'];
            dotnet.invokeMethodAsync(action, ...args)
                .catch(err => console.error('Note block reorder failed', err));
        }
        function tableEdgeIntent(e) {
            clearClasses('.note-table-col-intent,.note-table-row-intent', tableMarks);
            const frame = e.target.closest?.('.note-table-frame');
            if (!frame || !root.contains(frame)) return;
            const matrix = frame.querySelector('.note-table-matrix');
            if (!matrix) return;
            const rect = matrix.getBoundingClientRect();
            const nearTop = e.clientY >= rect.top - 42 && e.clientY <= rect.top + 5;
            const nearRight = e.clientX >= rect.right - 5 && e.clientX <= rect.right + 42;
            // Only one delete affordance at a time, chosen by the nearer edge.
            if (nearTop && (!nearRight || Math.abs(e.clientY - rect.top) <= Math.abs(e.clientX - rect.right))) {
                const cell = [...matrix.querySelectorAll('[data-note-table-row="0"]')]
                    .find(x => pointInRect(e.clientX, rect.top, x.getBoundingClientRect(), 1));
                cell?.classList.add('note-table-col-intent');
            } else if (nearRight) {
                const cell = [...matrix.querySelectorAll('[data-note-table-col="0"]')]
                    .find(x => pointInRect(rect.right, e.clientY, x.getBoundingClientRect(), 1));
                cell?.classList.add('note-table-row-intent');
            }
        }
        function onPointerMove(e) {
            if (dragging) return;
            clearClasses('.note-insert-row.note-hover-intent', ['note-hover-intent']);
            clearClasses('.note-side-right-intent,.note-side-left-intent', sideMarks);
            tableEdgeIntent(e);
            const block = e.target.closest?.('[data-note-block]');
            if (!block || !root.contains(block)) return;
            const rect = visibleCard(block).getBoundingClientRect();
            const row = block.closest('.note-layout-row');
            if (row) {
                const edge = (e.clientY - rect.top) / Math.max(rect.height, 1);
                const insert = edge < .18 ? row.previousElementSibling : edge > .82 ? row.nextElementSibling : null;
                if (insert?.classList.contains('note-insert-row')) insert.classList.add('note-hover-intent');
            }
            if (e.target.closest('.note-side-add,.note-side-picker')) {
                block.classList.add(e.target.closest('.note-side-right,.note-side-picker.right')
                    ? 'note-side-right-intent' : 'note-side-left-intent');
                return;
            }
            const horizontal = (e.clientX - rect.left) / Math.max(rect.width, 1);
            const vertical = (e.clientY - rect.top) / Math.max(rect.height, 1);
            if (vertical > .16 && vertical < .84) {
                if (horizontal >= .83 && horizontal < 1.13) block.classList.add('note-side-right-intent');
                if (horizontal <= .17 && horizontal > -.13) block.classList.add('note-side-left-intent');
            }
        }
        function onPointerLeave() {
            clearClasses('.note-insert-row.note-hover-intent', ['note-hover-intent']);
            clearClasses('.note-side-right-intent,.note-side-left-intent', sideMarks);
            clearClasses('.note-table-col-intent,.note-table-row-intent', tableMarks);
        }
        const onTextInput = e => {
            if (e.target.matches?.('.note-table-cell textarea')) {
                growTextarea(e.target);
                scheduleMeasure();
            }
        };
        // One document listener per mounted editor. Ignore clicks inside a picker or
        // on its trigger; the trigger's Blazor handler owns switching/toggling.
        function onDocumentPointerDown(e) {
            if (!root.isConnected || !root.querySelector('.note-block-picker')) return;
            const target = e.target;
            if (target instanceof Element && root.contains(target) &&
                target.closest('.note-block-picker,.note-add-button,.note-side-add')) return;
            dotnet.invokeMethodAsync('DismissInsertMenusAsync')
                .catch(err => console.error('Note insert menu dismissal failed', err));
        }
        function onDocumentKeyDown(e) {
            if (e.key !== 'Escape' || !root.querySelector('.note-block-picker')) return;
            // A containing dialog handles its own Escape first.
            if (document.querySelector('.app-modal-backdrop')) return;
            dotnet.invokeMethodAsync('DismissInsertMenusAsync')
                .catch(err => console.error('Note insert menu dismissal failed', err));
        }
        document.addEventListener('pointerdown', onDocumentPointerDown, true);
        document.addEventListener('keydown', onDocumentKeyDown);
        const mutation = new MutationObserver(() => scheduleMeasure());
        mutation.observe(root, { childList: true, subtree: true });
        for (const [name, listener] of [
            ['dragstart', onStart], ['dragover', onOver], ['drop', onDrop],
            ['dragend', cleanupDrag], ['pointermove', onPointerMove],
            ['pointerleave', onPointerLeave], ['input', onTextInput]
        ]) root.addEventListener(name, listener);
        scheduleMeasure();
        bindings.set(root, () => {
            document.removeEventListener('pointerdown', onDocumentPointerDown, true);
            document.removeEventListener('keydown', onDocumentKeyDown);
            mutation.disconnect();
            resize?.disconnect();
            for (const [name, listener] of [
                ['dragstart', onStart], ['dragover', onOver], ['drop', onDrop],
                ['dragend', cleanupDrag], ['pointermove', onPointerMove],
                ['pointerleave', onPointerLeave], ['input', onTextInput]
            ]) root.removeEventListener(name, listener);
            cleanupDrag();
            onPointerLeave();
        });
    }
    function detach(root) {
        bindings.get(root)?.();
        bindings.delete(root);
    }
    return { attach, detach };
})();
