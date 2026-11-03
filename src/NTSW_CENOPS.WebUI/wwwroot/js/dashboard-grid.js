// Glasswork 2.19 — RTL pixel canvas, no implicit CSS-grid tracks.
// A tile's right/top/width/height is independent of its neighbours.
// Blazor owns persistence; this module only stages geometry on a canvas.
window.cenopsDashboardCanvas = (() => {
    const states = new WeakMap();
    const GAP = 10;
    const MIN_W = 40;
    const MIN_H = 40;
    const minHeightFor = tile => keyOf(tile)?.startsWith('report:') ? 165 : MIN_H;
    const MOBILE = 620;
    const number = (value, fallback = 0) => Number.isFinite(+value) ? +value : fallback;
    const round = n => Math.round(n);
    const clamp = (n, min, max) => Math.max(min, Math.min(max, n));
    const keyOf = tile => tile?.dataset.dashTile;
    const tileOf = (grid, target) => {
        const el = target?.closest?.('[data-dash-tile]');
        return el?.parentElement === grid ? el : null;
    };
    const rectsOverlap = (a, b) => a.x < b.x + b.w + GAP &&
        a.x + a.w + GAP > b.x && a.y < b.y + b.h + GAP && a.y + a.h + GAP > b.y;
    const normalize = (p, canvas) => ({
        x: clamp(round(p.x), 0, Math.max(0, canvas - MIN_W)),
        y: Math.max(0, round(p.y)),
        w: clamp(round(p.w), MIN_W, Math.max(MIN_W, canvas - clamp(round(p.x), 0, canvas - MIN_W))),
        h: clamp(round(p.h), MIN_H, 10000)
    });
    const columnsAt = width => width < 640 ? 2 : width < 1100 ? 4 : 6;

    function firstAvailable(width, height, existing, canvas) {
        // Top-most possible placement. Candidate edges are exact pixel coordinates,
        // not shared rows; unequal heights leave independent gaps.
        const xs = [0, ...existing.map(p => p.x + p.w + GAP)]
            .filter(x => x + width <= canvas + 1).sort((a, b) => a - b);
        const ys = [0, ...existing.map(p => p.y + p.h + GAP)].sort((a, b) => a - b);
        for (const y of [...new Set(ys)]) {
            for (const x of [...new Set(xs)]) {
                const candidate = { x, y, w: width, h: height };
                if (existing.every(p => !rectsOverlap(candidate, p))) return candidate;
            }
        }
        return { x: 0, y: Math.max(0, ...existing.map(p => p.y + p.h + GAP)), w: width, h: height };
    }

    function readTiles(state) {
        const canvas = Math.max(200, round(state.grid.clientWidth));
        const visible = [...state.grid.querySelectorAll(':scope > [data-dash-tile]')];
        const entries = visible.map(el => ({ el, key: keyOf(el), data: el.dataset }));
        const placed = [];
        const map = new Map();
        for (const {el, key, data} of entries) {
            const pw = number(data.dashPw), ph = number(data.dashPh);
            const baseline = number(data.dashCw);
            if (!pw || !ph || !baseline) continue;
            const ratio = canvas / baseline;
            const p = normalize({ x: number(data.dashX) * ratio, y: number(data.dashY),
                w: pw * ratio, h: ph }, canvas);
            p.h = Math.max(p.h, minHeightFor(el));
            placed.push(p);
            map.set(key, { el, ...p });
        }
        // Legacy (w/h grid spans) and newly inserted cards get *one-time* pixel positions.
        // Preserve their order and avoid disturbing existing saved placements.
        const columns = columnsAt(canvas);
        const col = (canvas - (columns - 1) * GAP) / columns;
        for (const {el, key, data} of entries) {
            if (map.has(key)) continue;
            const span = clamp(number(data.dashW, 2), 1, columns);
            const w = round(col * span + GAP * (span - 1));
            const h = clamp(number(data.dashH, 4) * 54 + (number(data.dashH, 4) - 1) * GAP, MIN_H, 10000);
            const p = firstAvailable(w, h, placed, canvas);
            placed.push(p);
            map.set(key, { el, ...p });
        }
        state.items = map;
        state.canvas = canvas;
    }

    function draw(state) {
        if (state.active) return;
        const {grid, items} = state;
        grid.classList.add('dashboard-canvas-ready');
        if (grid.clientWidth <= MOBILE) {
            grid.classList.add('dashboard-canvas-mobile');
            grid.style.height = '';
            items.forEach(p => {p.el.style.cssText = '';});
            return;
        }
        grid.classList.remove('dashboard-canvas-mobile');
        let bottom = 0;
        items.forEach(p => {
            p.el.style.right = `${p.x}px`;
            p.el.style.top = `${p.y}px`;
            p.el.style.width = `${p.w}px`;
            p.el.style.height = `${p.h}px`;
            p.el.style.transform = '';
            bottom = Math.max(bottom, p.y + p.h);
        });
        grid.style.height = `${bottom}px`;
    }

    function cascade(state, protectedKeys) {
        // If a card grows/moves into occupied space, only intersected neighbours move.
        // They retain their own x coordinate and exact size; vertical gaps stay unequal.
        const all = [...state.items.values()];
        const protectedSet = new Set(protectedKeys);
        const stable = all.filter(p => protectedSet.has(keyOf(p.el)));
        const movable = all.filter(p => !protectedSet.has(keyOf(p.el)))
            .sort((a, b) => a.y - b.y || a.x - b.x);
        for (const p of movable) {
            let guard = 0;
            while (guard++ < all.length + 2) {
                const conflicting = stable.filter(q => rectsOverlap(p, q));
                if (!conflicting.length) break;
                p.y = Math.max(...conflicting.map(q => q.y + q.h + GAP));
            }
            stable.push(p);
        }
    }

    function makeSnapshot(state) {
        // Positions refer to the active RTL canvas's pixel width. Returning *all* tiles
        // also upgrades old grid-span layouts and initializes cards just added in the picker.
        return [...state.items].map(([key, p]) => ({key, x: round(p.x), y: round(p.y),
            width: round(p.w), height: round(p.h), canvasWidth: state.canvas}));
    }
    function commit(state) {
        if (!state.editable || state.saving) return;
        state.saving = true;
        const patch = makeSnapshot(state);
        state.dotnet.invokeMethodAsync('ApplyTileGeometryAsync', state.zone, patch)
            .catch(err => console.error('Dashboard canvas update failed:', err))
            .finally(() => {state.saving = false;});
    }
    function clearTarget(state) {
        state.grid.querySelectorAll('.dashboard-drop-swap,.dashboard-drop-edge').forEach(el =>
            el.classList.remove('dashboard-drop-swap', 'dashboard-drop-edge'));
        state.marker.hidden = true;
    }
    function clearInteraction(state) {
        const interaction = state.active;
        if (!interaction) return;
        state.active = null;
        state.grid.classList.remove('dashboard-canvas-moving', 'dashboard-canvas-sizing');
        interaction.tile.style.transform = '';
        interaction.tile.style.zIndex = '';
        interaction.tile.style.pointerEvents = '';
        interaction.tile.classList.remove('dashboard-dragging');
        clearTarget(state);
        document.body.classList.remove('dashboard-resizing');
        try { if (state.grid.hasPointerCapture(interaction.id)) state.grid.releasePointerCapture(interaction.id); } catch { /* element disconnected */ }
    }
    function edgeAt(pointer, targetRect) {
        const distances = [
            ['top', pointer.y - targetRect.top], ['bottom', targetRect.bottom - pointer.y],
            ['left', pointer.x - targetRect.left], ['right', targetRect.right - pointer.x]
        ].sort((a,b) => a[1]-b[1]);
        const threshold = Math.min(30, targetRect.width * .28, targetRect.height * .28);
        return distances[0][1] <= threshold ? distances[0][0] : 'swap';
    }
    function edgePosition(source, target, edge, canvas) {
        // 'x' increases towards the physical LEFT (RTL).
        if (edge === 'top') return {x:target.x, y:target.y-source.h-GAP};
        if (edge === 'bottom') return {x:target.x, y:target.y+target.h+GAP};
        if (edge === 'left') return {x:target.x+target.w+GAP, y:target.y};
        return {x:target.x-source.w-GAP, y:target.y};
    }
    function onPointerDown(state, e) {
        if (!state.editable || state.active || state.saving || state.grid.clientWidth <= MOBILE || e.button !== 0) return;
        const handle = e.target.closest?.('[data-dash-drag],[data-dash-resize]');
        const tile = tileOf(state.grid, handle);
        const source = tile && state.items.get(keyOf(tile));
        if (!handle || !source || !tile) return;
        e.preventDefault();
        e.stopPropagation();
        const mode = handle.hasAttribute('data-dash-drag') ? 'move' : handle.dataset.dashResize;
        state.active = { id:e.pointerId, tile, source, mode, startX:e.clientX, startY:e.clientY,
            origin:{x:source.x,y:source.y,w:source.w,h:source.h}, target:null, edge:null, moved:false };
        state.grid.setPointerCapture(e.pointerId);
        state.grid.classList.add(mode === 'move' ? 'dashboard-canvas-moving' : 'dashboard-canvas-sizing');
        if (mode === 'move') { tile.classList.add('dashboard-dragging'); tile.style.zIndex='20'; tile.style.pointerEvents='none'; }
        else document.body.classList.add('dashboard-resizing');
    }
    function onPointerMove(state, e) {
        const a = state.active;
        if (!a || a.id !== e.pointerId) return;
        e.preventDefault();
        const dx = e.clientX - a.startX, dy = e.clientY - a.startY;
        if (Math.abs(dx) + Math.abs(dy) > 2) a.moved = true;
        if (a.mode !== 'move') {
            a.source.w = a.mode.includes('x') ? clamp(round(a.origin.w - dx), MIN_W, state.canvas-a.origin.x) : a.origin.w;
            a.source.h = a.mode.includes('y') ? clamp(round(a.origin.h + dy), minHeightFor(a.tile), 10000) : a.origin.h;
            a.tile.style.width = `${a.source.w}px`;
            a.tile.style.height = `${a.source.h}px`;
            // Grow the canvas while dragging down, without moving neighbours mid-pointer.
            state.grid.style.height = `${Math.max(...[...state.items.values()].map(p=>p.y+p.h))}px`;
            return;
        }
        a.tile.style.transform = `translate(${dx}px,${dy}px)`;
        clearTarget(state);
        const hovered = tileOf(state.grid, document.elementFromPoint(e.clientX, e.clientY));
        const target = hovered && hovered !== a.tile ? state.items.get(keyOf(hovered)) : null;
        a.target = target;
        a.edge = target ? edgeAt({x:e.clientX,y:e.clientY}, hovered.getBoundingClientRect()) : null;
        if (target) hovered.classList.add(a.edge === 'swap' ? 'dashboard-drop-swap' : 'dashboard-drop-edge');
        const pos = target && a.edge !== 'swap'
            ? edgePosition(a.source, target, a.edge, state.canvas)
            : {x:a.origin.x-dx, y:a.origin.y+dy};
        if (a.edge === 'swap' && target) {
            state.marker.style.right = `${target.x}px`;
            state.marker.style.top = `${target.y}px`;
            state.marker.style.width = `${target.w}px`;
            state.marker.style.height = `${target.h}px`;
        } else {
            state.marker.style.right = `${clamp(round(pos.x),0,Math.max(0,state.canvas-a.source.w))}px`;
            state.marker.style.top = `${Math.max(0,round(pos.y))}px`;
            state.marker.style.width = `${a.source.w}px`;
            state.marker.style.height = `${a.source.h}px`;
        }
        state.marker.hidden = false;
    }
    function onPointerEnd(state, e, cancelled = false) {
        const a = state.active;
        if (!a || a.id !== e.pointerId) return;
        // Do not lose the last movement when pointerup arrives without pointermove.
        const dx = e.clientX-a.startX, dy=e.clientY-a.startY;
        if (a.mode === 'move' && a.moved && !cancelled) {
            if (a.target && a.edge === 'swap') {
                // Swap complete rectangles: each card adopts the other's *slot geometry*,
                // including width and height. The content follows its card identity.
                const destination = {x:a.target.x, y:a.target.y, w:a.target.w, h:a.target.h};
                const origin = {...a.origin};
                Object.assign(a.source, destination);
                Object.assign(a.target, origin);
                cascade(state, [keyOf(a.tile), keyOf(a.target.el)]);
            } else {
                const intended = a.target && a.edge
                    ? edgePosition(a.source,a.target,a.edge,state.canvas)
                    : {x:a.origin.x-dx,y:a.origin.y+dy};
                a.source.x = clamp(round(intended.x),0,Math.max(0,state.canvas-a.source.w));
                a.source.y = Math.max(0,round(intended.y));
                cascade(state,[keyOf(a.tile)]);
            }
        } else if (a.mode !== 'move' && !cancelled) {
            cascade(state,[keyOf(a.tile)]);
        } else if (a.mode !== 'move') {
            Object.assign(a.source,a.origin);
        }
        const changed = !cancelled && (a.mode === 'move' ? a.moved
            : a.source.w !== a.origin.w || a.source.h !== a.origin.h);
        clearInteraction(state);
        draw(state);
        if (changed) commit(state);
    }
    function keyboard(state, e) {
        if (!state.editable || !['ArrowLeft','ArrowRight','ArrowUp','ArrowDown'].includes(e.key)) return;
        const handle = e.target.closest?.('[data-dash-drag],[data-dash-resize]');
        const tile = tileOf(state.grid, handle);
        const p = tile && state.items.get(keyOf(tile));
        if (!handle || !p) return;
        const delta = e.shiftKey ? 1 : 10;
        if (handle.hasAttribute('data-dash-drag') && !e.altKey) return;
        e.preventDefault();
        if (handle.hasAttribute('data-dash-drag')) {
            p.x=clamp(p.x+(e.key==='ArrowLeft'?delta:e.key==='ArrowRight'?-delta:0),0,state.canvas-p.w);
            p.y=Math.max(0,p.y+(e.key==='ArrowDown'?delta:e.key==='ArrowUp'?-delta:0));
        } else {
            const mode = handle.dataset.dashResize;
            if (mode.includes('x')) p.w=clamp(p.w+(e.key==='ArrowLeft'?delta:e.key==='ArrowRight'?-delta:0),MIN_W,state.canvas-p.x);
            if (mode.includes('y')) p.h=clamp(p.h+(e.key==='ArrowDown'?delta:e.key==='ArrowUp'?-delta:0),MIN_H,10000);
        }
        cascade(state,[keyOf(tile)]);
        draw(state);
        commit(state);
    }
    function mount(grid, dotnet, zone, editable) {
        if (!grid) return;
        let state = states.get(grid);
        if (!state) {
            let marker = grid.querySelector(':scope > .dashboard-canvas-indicator');
            if (!marker) {
                marker = document.createElement('div');
                marker.className='dashboard-canvas-indicator';
                marker.hidden=true;marker.setAttribute('aria-hidden','true');
                grid.append(marker);
            }
            state={grid,marker,items:new Map(),zone,dotnet,editable,active:null,saving:false,canvas:0};
            states.set(grid,state);
            grid.addEventListener('pointerdown',e=>onPointerDown(state,e));
            grid.addEventListener('pointermove',e=>onPointerMove(state,e));
            grid.addEventListener('pointerup',e=>onPointerEnd(state,e));
            grid.addEventListener('pointercancel',e=>onPointerEnd(state,e,true));
            grid.addEventListener('lostpointercapture',e=>onPointerEnd(state,e,true));
            grid.addEventListener('keydown',e=>keyboard(state,e));
            const parent=grid.parentElement;
            if (typeof ResizeObserver !== 'undefined' && parent) {
                let width=parent.clientWidth;
                state.observer=new ResizeObserver(()=>{
                    if (!grid.isConnected) {state.observer.disconnect();return;}
                    if (parent.clientWidth===width || state.active) return;
                    width=parent.clientWidth;
                    readTiles(state);draw(state);
                });
                state.observer.observe(parent);
            }
        }
        state.zone=zone;state.dotnet=dotnet;state.editable=!!editable;
        if (!state.active) {readTiles(state);draw(state);}
    }
    function snapshot(grid) {
        const state=grid && states.get(grid);
        if (!state) return [];
        if (grid.clientWidth <= MOBILE) {
            // The narrow-screen list is a display fallback. Saving a name/card change
            // there must not shrink the desktop canvas or overwrite its coordinates.
            return [...grid.querySelectorAll(':scope > [data-dash-tile]')].flatMap(el => {
                const d=el.dataset;
                return number(d.dashPw) > 0 && number(d.dashPh) > 0 && number(d.dashCw) > 0
                    ? [{key:d.dashTile, x:number(d.dashX), y:number(d.dashY),
                        width:number(d.dashPw), height:number(d.dashPh), canvasWidth:number(d.dashCw)}]
                    : [];
            });
        }
        // The live canvas is authoritative until the async Blazor callback completes.
        // Rereading data attributes here would silently undo a fast resize -> Save.
        return makeSnapshot(state);
    }
    return {mount,snapshot};
})();
