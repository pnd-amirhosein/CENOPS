// Calendar Escape handling precedes the generic AppModal Escape handler.
// The inner date picker closes without accidentally dismissing its parent form.
window.addEventListener('keydown', event => {
    if (event.key !== 'Escape') return;
    const calendar = document.querySelector('.jalali-picker .jalali-popover');
    if (!calendar) return;
    event.preventDefault();
    event.stopImmediatePropagation();
    calendar.closest('.jalali-picker')?.querySelector('.jalali-backdrop')?.click();
}, true);

// Smart-card configuration owns Escape, including its nested content browser.
window.addEventListener('keydown', function (event) {
    if (event.key !== 'Escape' || document.querySelector('.app-modal-backdrop')) return;
    const modal = document.querySelector('.dash-smart-config-modal');
    if (!modal) return;
    event.preventDefault();
    event.stopImmediatePropagation();
    modal.querySelector('[data-smart-escape-action]')?.click();
}, true);

// Dashboard content picker owns Escape while open. Trigger the Blazor handler so
// previews, selection popovers and the unsaved-selection confirmation cooperate.
window.addEventListener('keydown', function (event) {
    if (event.key !== 'Escape') return;
    if (document.querySelector('.app-modal-backdrop, .cenops-chat-window, .dash-smart-config-modal')) return;
    const confirm = document.querySelector('.dashboard-confirm-backdrop');
    if (confirm) {
        event.preventDefault();
        event.stopImmediatePropagation();
        confirm.querySelector('[data-dashboard-confirm-dismiss]')?.click();
        return;
    }
    const standalonePreview = document.querySelector('.dcp-detail-modal');
    const picker = document.querySelector('.dcp-backdrop');
    if (!picker && standalonePreview) {
        event.preventDefault();
        event.stopImmediatePropagation();
        standalonePreview.querySelector('.dcp-close')?.click();
        return;
    }
    if (!picker) return;
    event.preventDefault();
    event.stopImmediatePropagation();
    picker.querySelector('[data-dcp-escape-action]')?.click();
}, true);

// Modal keyboard policy (capture phase): only the topmost AppModal owns Escape.
// CloseOnEscape=false suppresses Escape instead of triggering close shortcuts behind it.
window.addEventListener('keydown', function (event) {
    if (event.key !== 'Escape') return;
    // A searchable select inside a modal owns Escape while its popup is open.
    if (document.querySelector('.gw-select-popover:not([hidden])')) return;
    const modals = document.querySelectorAll('.app-modal-backdrop');
    const top = modals[modals.length - 1];
    if (!top) return;
    event.preventDefault();
    event.stopImmediatePropagation();
    if (top.dataset.appModalEscape === 'true') {
        top.querySelector('.app-modal-escape-trigger')?.click();
    }
}, true);

// Chrome interprets Ctrl+wheel as browser zoom. While a report explorer is
// open, reserve that gesture for its chart (or simply suppress it if empty).
// The chart interaction module also receives this event to zoom its X range.
window.addEventListener('wheel', function (event) {
    if (event.ctrlKey && document.querySelector('.monitor-chart-modal')) {
        event.preventDefault();
    }
}, { capture: true, passive: false });

// Close the CSS-powered mobile drawer after choosing a route. Event delegation
// survives Blazor's enhanced navigation and interactive component rerenders.
document.addEventListener("click", function (event) {
    if (!(event.target instanceof Element)) return;
    if (!event.target.closest(".app-shell .sidebar-navigation a.nav-link")) return;

    const toggle = document.getElementById("shell-nav-toggle");
    if (toggle) toggle.checked = false;
});

// Chat owns keyboard commands while its modal is visible. Register this capture
// handler BEFORE the app-wide Ctrl+K and Skyline listeners, so commands cannot
// leak through to the background workspace. All actions click the existing
// Blazor buttons, retaining their existing authorization and UI behavior.
(function () {
    let chordTimer = null;
    let armed = false;
    const modal = () => document.querySelector('.cenops-chat-window');

    function disarm() {
        armed = false;
        document.body.classList.remove('cenops-chat-chord-armed');
        if (chordTimer !== null) window.clearTimeout(chordTimer);
        chordTimer = null;
    }
    function arm() {
        disarm();
        armed = true;
        document.body.classList.add('cenops-chat-chord-armed');
        // A chord is intentionally short-lived, like an editor command sequence.
        chordTimer = window.setTimeout(disarm, 5000);
    }
    function swallow(event) {
        event.preventDefault();
        event.stopImmediatePropagation();
    }
    function isEditing(target) {
        return target instanceof Element && !!target.closest(
            'input, textarea, select, [contenteditable=""], [contenteditable="true"], [role="textbox"]');
    }

    window.addEventListener('keydown', function (event) {
        const chat = modal();
        if (!chat) {
            if (armed) disarm();
            return;
        }
        const key = window.cenopsShortcutKey(event);
        const command = (event.ctrlKey || event.metaKey) && !event.altKey && !event.shiftKey;

        if (event.isComposing) return;
        // Escape closes chat from anywhere (including focused message editors).
        // Use the existing Blazor close handler rather than mutating the DOM directly.
        if (key === 'escape') {
            swallow(event);
            disarm();
            // Nested chat dialogs own Escape; only close the workspace itself
            // when no smaller context is open.
            const dialogClosers = chat.querySelectorAll('.chat-dialog-shade [data-chat-close]');
            const nested = (dialogClosers.length && dialogClosers[dialogClosers.length - 1])
                || chat.querySelector('.chat-spotlight-esc')
                || chat.querySelector('.chat-details [data-chat-close]');
            (nested || chat.querySelector('.chat-modal-close'))?.click();
            return;
        }
        if (command && key === 'k') {
            swallow(event);
            if (!event.repeat) arm();
            return;
        }
        // None of the Skyline commands are allowed through an open chat window.
        if ((command && ['b', 'm', 'n', 'i'].includes(key)) ||
            (event.altKey && !event.ctrlKey && !event.metaKey && key === 'n')) {
            swallow(event);
            disarm();
            return;
        }
        if (armed) {
            if (key === 'escape') {
                swallow(event);
                disarm();
                return;
            }
            if (!event.ctrlKey && !event.metaKey && !event.altKey && !event.shiftKey &&
                (key === 'n' || key === 's')) {
                swallow(event);
                disarm();
                const button = chat.querySelector(key === 'n' ? '.chat-rail-new' : '.chat-rail-search');
                button?.click();
                return;
            }
            // An unsupported key ends the pending sequence; otherwise it could
            // trigger unexpectedly several keystrokes later.
            if (key.length === 1 && !event.ctrlKey && !event.metaKey && !event.altKey) disarm();
        }
        // Even if a Skyline menu was open when chat launched, its N/C/S/R/T/A
        // shortcuts must never navigate away from the modal.
        if (!isEditing(event.target) && !event.ctrlKey && !event.metaKey &&
            !event.altKey && !event.shiftKey && ['n', 'c', 's', 'r', 't', 'a', 'e'].includes(key) &&
            document.querySelector('.app-shell .skyline-create-menu')) {
            event.stopImmediatePropagation();
        }
    }, true);
    // Blazor replaces the modal node when closed; remove any stale key hints.
    const observer = new MutationObserver(function () {
        if (armed && !modal()) disarm();
    });
    observer.observe(document.body, {childList:true, subtree:true});
})();

// Workspace command search: only active inside the authenticated shell.
(function () {
    document.addEventListener('keydown', function (event) {
        if (!(event.metaKey || event.ctrlKey) || window.cenopsShortcutKey(event) !== 'k') return;
        var trigger = document.querySelector('.app-shell .quick-search-trigger');
        if (!trigger) return;
        event.preventDefault();
        if (trigger.getAttribute('aria-expanded') !== 'true') trigger.click();
        window.requestAnimationFrame(function () {
            var input = document.querySelector('.app-shell #quick-search-input');
            if (input) input.focus();
        });
    });
    // Focus search input when a command-search dialog is inserted by Blazor.
    var observer = new MutationObserver(function (records) {
        if (!records.some(function (record) { return record.addedNodes.length > 0; })) return;
        var panel = document.querySelector('.app-shell .quick-search-panel');
        var input = panel && panel.querySelector('input');
        if (input && !input.dataset.opsAutoFocused) { input.dataset.opsAutoFocused = '1'; input.focus(); }
    });
    observer.observe(document.body, { childList: true, subtree: true });
})();

// Match the physical shortcut key regardless of active Persian/English keyboard.
// The fallback supports virtual keyboards that do not expose an event.code.
window.cenopsShortcutKey = function (event) {
    const code = event.code || '';
    if (/^Key[A-Z]$/.test(code)) return code.slice(3).toLowerCase();
    const character = (event.key || '').toLowerCase();
    const persian = { 'ئ':'m', 'ش':'a', 'س':'s', 'ی':'d', 'ب':'f',
        'ل':'g', 'ا':'h', 'ت':'j', 'ن':'k', 'م':'l', 'ض':'q', 'ص':'w',
        'ث':'e', 'ق':'r', 'ف':'t', 'غ':'y', 'ع':'u', 'ه':'i',
        'خ':'o', 'ح':'p', 'ظ':'z', 'ط':'x', 'ز':'c', 'ر':'v',
        'ذ':'b', 'د':'n' };
    return persian[character] || character;
};

// Skyline shortcuts: use the same real Blazor button handlers as mouse clicks.
// Ctrl+B opens the creation menu. Keyboard actions call the same authorized
// Blazor methods as their corresponding Skyline buttons.
(function () {
    const isEditing = (target) => target instanceof Element && !!target.closest(
        'input, textarea, select, [contenteditable=""], [contenteditable="true"], [role="textbox"]');

    window.addEventListener('keydown', function (event) {
        if (event.defaultPrevented || event.isComposing || event.repeat) return;
        const key = window.cenopsShortcutKey(event);
        const shell = document.querySelector('.app-shell');
        if (!shell) return;
        const create = shell.querySelector('.skyline-create-button');
        const tools = shell.querySelector('.skyline-tools-button');
        const chat = shell.querySelector('.skyline-chat-launch');
        const ctrlOrCmd = (event.ctrlKey || event.metaKey) && !event.altKey && !event.shiftKey;
        
        if (ctrlOrCmd && key === 'b' && create) {
            event.preventDefault();
            event.stopImmediatePropagation();
            create.click(); // Calls ToggleCreateAsync; permissions are reloaded server-side.
            return;
        }
        if (ctrlOrCmd && key === 'i' && tools) {
            event.preventDefault();
            event.stopImmediatePropagation();
            tools.click();
            return;
        }
        if (ctrlOrCmd && key === 'm' && chat) {
            event.preventDefault();
            event.stopImmediatePropagation();
            chat.click(); // Calls OpenChatAsync; chat.use is rechecked on the server.
            return;
        }
        if (event.ctrlKey || event.metaKey || event.altKey || event.shiftKey || isEditing(event.target)) return;
        const toolMenu = shell.querySelector('.skyline-tools-menu');
        if (toolMenu) {
            if (key === 'escape') {
                event.preventDefault();
                shell.querySelector('.skyline-tools-scrim')?.click();
                return;
            }
            const action = toolMenu.querySelector('[data-tool-shortcut="' + key + '"]');
            if (action && !action.disabled) {
                event.preventDefault();
                event.stopImmediatePropagation();
                action.click();
                return;
            }
            return;
        }
        const menu = shell.querySelector('.skyline-create-menu:not(.skyline-tools-menu)');
        if (!menu) return;
        if (key === 'escape') {
            event.preventDefault();
            shell.querySelector('.skyline-create-scrim')?.click();
            return;
        }
        if (!['n', 'c', 's', 'r', 't', 'a', 'e'].includes(key)) return;
        const item = menu.querySelector('[data-create-shortcut="' + key + '"]');
        if (!item || item.disabled) return; // Only server-granted options are rendered.
        event.preventDefault();
        event.stopImmediatePropagation();
        item.click(); // Calls NavigateCreateAsync, which rechecks live access.
    }, true);
})();

// Positions a DataTable filter in viewport coordinates, outside its scroll layout.
window.cenopsUI = window.cenopsUI || {};
// The .NET 8 custom reconnection overlay is static HTML, so this works even
// while Blazor's interactive circuit is unavailable.
window.cenopsUI.retryConnection = async function () {
    const retryButton = document.querySelector('.cenops-reconnect-retry');
    if (retryButton) retryButton.disabled = true;
    try {
        if (!window.Blazor || typeof window.Blazor.reconnect !== 'function') {
            window.location.reload();
            return;
        }

        const reconnected = await window.Blazor.reconnect();
        if (!reconnected) window.location.reload();
    } catch {
        if (retryButton) retryButton.disabled = false;
    }
};
window.cenopsUI.getFilterPosition = function (trigger) {
    const bounds = trigger.getBoundingClientRect();
    const viewportWidth = window.innerWidth;
    const viewportHeight = window.innerHeight;
    const width = Math.min(680, viewportWidth - 24);
    const left = Math.max(12, Math.min(bounds.right - width, viewportWidth - width - 12));
    const below = Math.max(0, viewportHeight - bounds.bottom - 20);
    const above = Math.max(0, bounds.top - 20);
    const openUp = below < 250 && above > below;
    const available = openUp ? above : below;
    const maxHeight = Math.max(85, Math.floor(Math.min(560, available, viewportHeight - 24)));
    const top = openUp ? Math.max(12, Math.round(bounds.top - maxHeight - 10))
                       : Math.round(bounds.bottom + 10);
    return { left: Math.round(left), top: top, maxHeight: maxHeight };
};


// A 500ms welcome transition once after signing in, never on every navigation.
// Return to the login/signup/password screens resets this browser-tab flag.
(function () {
    const key = "cenops:intro-completed";
    if (/^\/(login|signup|change-password)(?:\/|$)/i.test(window.location.pathname)) {
        try { window.sessionStorage.removeItem(key); } catch { /* storage unavailable */ }
    }
    window.cenopsUI = window.cenopsUI || {};
    window.cenopsUI.shouldShowSignInIntro = function () {
        try {
            if (window.sessionStorage.getItem(key) === "1") return false;
            window.sessionStorage.setItem(key, "1");
        } catch { /* Private browsing: still allow the transition. */ }
        return true;
    };
})();
