// Temporary same-tab snippet transfer: never place code in the address bar.
window.cenopsSnippetDraft = (() => {
    const prefix = 'cenops:snippet-draft:';
    const maxAgeMs = 10 * 60 * 1000;
    function store(kind, content, language = 'plain', blockKind = 'code') {
        if (!['note', 'sql', 'curl'].includes(kind) || typeof content !== 'string' || content.length > 50000)
            throw new Error('Invalid snippet draft');
        const id = crypto.randomUUID();
        sessionStorage.setItem(prefix + id, JSON.stringify({ Kind: kind, Content: content, Language: language,
            BlockKind: blockKind, Created: Date.now() }));
        return id;
    }
    function take(id) {
        if (typeof id !== 'string' || !/^[0-9a-f-]{36}$/i.test(id)) return null;
        const key = prefix + id;
        const json = sessionStorage.getItem(key);
        sessionStorage.removeItem(key);
        if (!json) return null;
        try {
            const value = JSON.parse(json);
            if (!value || Date.now() - value.Created > maxAgeMs || value.Content?.length > 50000) return null;
            return value;
        } catch { return null; }
    }
    return { store, take, focus: element => element?.focus() };
})();
