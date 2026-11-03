// Shared keyboard affordances for graph renderers. Never assume a particular toolbar.
export function attachGraphModifierBadges(root) {
    const update = event => {
        root.classList.toggle('graph-shift-held', event.shiftKey || (event.type === 'keydown' && event.key === 'Shift'));
        root.classList.toggle('graph-ctrl-held', event.ctrlKey || event.metaKey || (event.type === 'keydown' && ['Control', 'Meta'].includes(event.key)));
    };
    const clear = () => root.classList.remove('graph-shift-held', 'graph-ctrl-held');
    document.addEventListener('keydown', update);
    document.addEventListener('keyup', update);
    window.addEventListener('blur', clear);
    return () => {
        document.removeEventListener('keydown', update);
        document.removeEventListener('keyup', update);
        window.removeEventListener('blur', clear);
        clear();
    };
}

// Linear, bounded, label-aware undo history. The cursor can jump to *any* prior
// snapshot without erasing the redo branch. Only a new edit truncates redo.
// The renderer owns graph snapshots; this helper owns history navigation.
export function createGraphHistory(capture, restore, notify, limit = 80) {
    let entries = [{ snapshot: capture(), label: 'شروع نمودار' }];
    let cursor = 0;
    const report = () => notify(cursor > 0, cursor < entries.length - 1,
        entries.map((entry, index) => ({ index, label: entry.label })), cursor);
    report();
    const goTo = index => {
        if (!Number.isInteger(index) || index < 0 || index >= entries.length || index === cursor) return;
        cursor = index;
        restore(entries[cursor].snapshot);
        report();
    };
    return {
        record(label = 'تغییر نمودار') {
            entries = entries.slice(0, cursor + 1);
            entries.push({ snapshot: capture(), label });
            if (entries.length > limit) entries.shift();
            cursor = entries.length - 1;
            report();
        },
        reset() { entries = [{ snapshot: capture(), label: 'شروع نمودار' }]; cursor = 0; report(); },
        undo() { goTo(cursor - 1); },
        redo() { goTo(cursor + 1); },
        goTo
    };
}
