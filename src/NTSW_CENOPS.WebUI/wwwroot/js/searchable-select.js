/* Glasswork 2.13.1 — progressive searchable select enhancement.
   The native <select> remains the source of truth for Blazor bind/change events,
   validation, form submission and disabled state. A single popover lives outside
   the Blazor-rendered subtree to avoid interfering with Razor DOM reconciliation. */
(() => {
    'use strict';
    let active = null;
    const escape = value => String(value ?? '');
    const root = document.createElement('div');
    root.className = 'gw-select-popover';
    root.hidden = true;
    root.setAttribute('role', 'dialog');
    root.setAttribute('aria-label', 'جستجو در گزینه‌ها');
    root.innerHTML = `<div class="gw-select-search-row"><svg viewBox="0 0 24 24" aria-hidden="true" fill="none" stroke="currentColor" stroke-width="1.8"><circle cx="10.8" cy="10.8" r="6.6"/><path d="m16 16 5 5"/></svg><input class="gw-select-search" type="search" autocomplete="off" placeholder="جستجو در گزینه‌ها…" aria-label="جستجوی گزینه‌ها" /></div><div class="gw-select-options" role="listbox"></div>`;
    document.body.append(root);
    const search = root.querySelector('.gw-select-search');
    const list = root.querySelector('.gw-select-options');
    let items = [];
    let highlighted = 0;

    function visible(select) {
        return select instanceof HTMLSelectElement && select.isConnected && !select.disabled && !select.multiple && select.size <= 1 && select.getClientRects().length > 0 && !select.hasAttribute('data-search-disabled');
    }
    function reposition() {
        if (!active) return;
        if (!visible(active)) { close(); return; }
        const bounds = active.getBoundingClientRect();
        const width = Math.max(230, Math.min(Math.max(bounds.width, 265), window.innerWidth - 20));
        const left = Math.max(10, Math.min(bounds.right - width, window.innerWidth - width - 10));
        root.style.width = `${width}px`;
        root.style.left = `${left}px`;
        const availableBelow = window.innerHeight - bounds.bottom - 12;
        const availableAbove = bounds.top - 12;
        const above = availableBelow < 215 && availableAbove > availableBelow;
        root.classList.toggle('gw-open-above', above);
        root.style.maxHeight = `${Math.max(140, Math.min(340, (above ? availableAbove : availableBelow) - 5))}px`;
        if (above) {
            root.style.top = 'auto';
            root.style.bottom = `${window.innerHeight - bounds.top + 5}px`;
        } else {
            root.style.bottom = 'auto';
            root.style.top = `${bounds.bottom + 5}px`;
        }
    }
    function close({ returnFocus = false } = {}) {
        const previous = active;
        active = null;
        root.hidden = true;
        root.classList.remove('gw-open-above');
        items = [];
        list.replaceChildren();
        if (previous) previous.removeAttribute('aria-expanded');
        if (returnFocus && previous?.isConnected) previous.focus({ preventScroll: true });
    }
    function choose(option) {
        if (!active || !option || option.disabled) return;
        const select = active;
        // Value setter and real change event preserve @bind, @onchange, validation and forms.
        select.value = option.value;
        select.dispatchEvent(new Event('change', { bubbles: true }));
        close({ returnFocus: true });
    }
    function paint() {
        if (!active) return;
        const query = search.value.trim().toLocaleLowerCase();
        items = Array.from(active.options).filter(x => !x.hidden && !x.parentElement?.disabled &&
            (!query || x.textContent.toLocaleLowerCase().includes(query)));
        highlighted = Math.min(highlighted, Math.max(0, items.length - 1));
        const frag = document.createDocumentFragment();
        if (!items.length) {
            const empty = document.createElement('div');
            empty.className = 'gw-select-empty';
            empty.textContent = 'گزینه‌ای پیدا نشد';
            frag.append(empty);
        }
        for (let i = 0; i < items.length; i++) {
            const opt = items[i];
            const row = document.createElement('button');
            row.type = 'button';
            row.className = 'gw-select-option';
            row.setAttribute('role', 'option');
            row.setAttribute('aria-selected', String(opt.value === active.value));
            row.dataset.index = String(i);
            row.disabled = opt.disabled;
            if (i === highlighted) row.classList.add('gw-highlight');
            const label = document.createElement('span');
            label.textContent = escape(opt.textContent);
            const tick = document.createElement('span');
            tick.className = 'gw-select-tick';
            tick.textContent = opt.value === active.value ? '✓' : '';
            row.append(label, tick);
            frag.append(row);
        }
        list.replaceChildren(frag);
    }
    function open(select) {
        if (!visible(select)) return;
        if (active === select) { close(); return; }
        close();
        active = select;
        search.value = '';
        highlighted = Math.max(0, Array.from(select.options).findIndex(x => x.value === select.value));
        root.hidden = false;
        root.dir = select.closest('[dir]')?.dir || document.documentElement.dir || 'rtl';
        select.setAttribute('aria-expanded', 'true');
        paint();
        reposition();
        search.focus({ preventScroll: true });
        list.querySelector('[aria-selected="true"]')?.scrollIntoView({ block: 'nearest' });
    }
    document.addEventListener('pointerdown', event => {
        const select = event.target instanceof Element ? event.target.closest('select') : null;
        if (select && visible(select)) {
            // Prevent native platform dropdown; keep actual select in place for semantics.
            event.preventDefault();
            open(select);
        } else if (active && !root.contains(event.target)) {
            close();
        }
    }, true);
    document.addEventListener('click', event => {
        const select = event.target instanceof Element ? event.target.closest('select') : null;
        if (!select || !visible(select)) return;
        event.preventDefault();
        // Keyboard/programmatic click fallback; pointer clicks were handled above.
        if (event.detail === 0) open(select);
    }, true);
    document.addEventListener('keydown', event => {
        if (active && (event.key === 'Escape' || event.key === 'Tab')) {
            if (event.key === 'Escape') event.preventDefault();
            close({ returnFocus: event.key === 'Escape' });
            return;
        }
        const select = event.target instanceof HTMLSelectElement ? event.target : null;
        if (select && visible(select) && ['Enter', ' ', 'ArrowDown', 'ArrowUp'].includes(event.key)) {
            event.preventDefault();
            open(select);
        }
    }, true);
    search.addEventListener('input', () => { highlighted = 0; paint(); });
    search.addEventListener('keydown', event => {
        if (!active) return;
        if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
            event.preventDefault();
            if (!items.length) return;
            const delta = event.key === 'ArrowDown' ? 1 : -1;
            highlighted = (highlighted + delta + items.length) % items.length;
            paint();
            list.querySelector('.gw-highlight')?.scrollIntoView({ block: 'nearest' });
        } else if (event.key === 'Enter') {
            event.preventDefault();
            choose(items[highlighted]);
        }
    });
    list.addEventListener('click', event => {
        const button = event.target instanceof Element ? event.target.closest('button[data-index]') : null;
        if (button) choose(items[Number(button.dataset.index)]);
    });
    document.addEventListener('scroll', () => { if (active) reposition(); }, true);
    window.addEventListener('resize', () => { if (active) reposition(); });
    document.addEventListener('change', event => {
        if (active && event.target === active) close();
    });
    // Internal native selection remains available without JavaScript.
})();
