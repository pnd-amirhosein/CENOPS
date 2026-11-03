// Glasswork 2.20 - scoped, delegated editor shortcuts. No global document key handlers.
window.cenopsWikiShortcuts = (() => {
    const active = new WeakMap();
    const nextFrame = callback => requestAnimationFrame(() => requestAnimationFrame(callback));

    function attach(root, bridge, kind) {
        if (!root || active.has(root)) return;
        const editable = e => e.target?.closest?.('input,textarea,[contenteditable="true"]');
        const focusItem = (key) => {
            if (!key) return;
            // Keys are GUIDs returned by the editor, not arbitrary CSS selectors.
            const row = [...root.querySelectorAll('[data-note-block]')].find(x => x.dataset.noteBlock === key);
            const input = row?.querySelector('.todo-input');
            if (input) { input.focus(); input.setSelectionRange(input.value.length, input.value.length); }
        };
        const onKeyDown = async e => {
            if (e.defaultPrevented || e.isComposing || e.ctrlKey || e.altKey || e.metaKey) return;
            if (!editable(e)) return;
            if (e.shiftKey && e.key === 'Enter') {
                e.preventDefault();
                try { await bridge.invokeMethodAsync('RequestShortcutSaveAsync'); }
                catch (err) { console.error('Wiki save shortcut failed:', err); }
                return;
            }
            if (kind !== 'todo') return;
            const input = e.target?.closest?.('.todo-input,.todo-check');
            if (!input) return;
            const row = input.closest('[data-note-block]');
            const rows = [...root.querySelectorAll('[data-note-block]')];
            const index = rows.indexOf(row);
            if (index < 0) return;
            if (e.shiftKey && e.code === 'Space') {
                e.preventDefault();
                row.querySelector('.todo-check')?.click();
                input.focus();
                return;
            }
            if (e.key !== 'ArrowUp' && e.key !== 'ArrowDown') return;
            e.preventDefault();
            const direction = e.key === 'ArrowUp' ? -1 : 1;
            if (!e.shiftKey && index + direction >= 0 && index + direction < rows.length) {
                focusItem(rows[index + direction].dataset.noteBlock);
                return;
            }
            if (!e.shiftKey && direction < 0) return;
            // Shift+arrow adds a row on either side, Down on last row adds at end.
            try {
                const inserted = await bridge.invokeMethodAsync('InsertViaShortcutAsync', row.dataset.noteBlock, direction < 0);
                nextFrame(() => focusItem(inserted));
            } catch (err) { console.error('Todo insert shortcut failed:', err); }
        };
        root.addEventListener('keydown', onKeyDown);
        active.set(root, () => root.removeEventListener('keydown', onKeyDown));
    }
    function detach(root) { active.get(root)?.(); active.delete(root); }
    return {attach, detach};
})();
