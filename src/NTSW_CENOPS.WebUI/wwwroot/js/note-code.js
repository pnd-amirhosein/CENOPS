// Native textarea owns typing and cursor position. Blazor owns only the model and
// the read-only highlighted layer. This prevents caret jumps on each keystroke.
window.cenopsNoteCode = {
    syncScroll(textarea) {
        const highlight = textarea?.parentElement?.querySelector('.note-code-highlight');
        if (!highlight) return;
        highlight.scrollTop = textarea.scrollTop;
        highlight.scrollLeft = textarea.scrollLeft;
    },
    syncValue(textarea, value, force = false) {
        if (!textarea) return;
        const next = value ?? '';
        // Ignore delayed parent echoes while the user is typing. Formatting is
        // an explicit, force-synchronized exception.
        if (document.activeElement === textarea && !force) return;
        const previous = textarea.value;
        if (previous === next) return;
        const focused = document.activeElement === textarea;
        const start = focused ? textarea.selectionStart : 0;
        const end = focused ? textarea.selectionEnd : 0;
        const direction = focused ? textarea.selectionDirection : 'none';
        // Map selection through the changed range rather than sending it to EOF.
        let prefix = 0;
        while (prefix < previous.length && prefix < next.length && previous[prefix] === next[prefix]) prefix++;
        let suffix = 0;
        while (suffix < previous.length - prefix && suffix < next.length - prefix &&
               previous[previous.length - 1 - suffix] === next[next.length - 1 - suffix]) suffix++;
        function map(position) {
            if (position <= prefix) return position;
            if (position >= previous.length - suffix) return Math.max(0, next.length - (previous.length - position));
            return prefix + Math.min(position - prefix, next.length - prefix - suffix);
        }
        const top = textarea.scrollTop, left = textarea.scrollLeft;
        textarea.value = next;
        if (focused) textarea.setSelectionRange(map(start), map(end), direction);
        textarea.scrollTop = top;
        textarea.scrollLeft = left;
        this.syncScroll(textarea);
    }
};
