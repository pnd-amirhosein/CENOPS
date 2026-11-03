// Glasswork report explorer: chart-local hover and selection, plus modal-scoped
// Ctrl+wheel zoom. All listeners belong to a single AbortController and are
// disposed whenever the Blazor SVG is rerendered or removed.
const attached = new Map();
const clamp = (value, min, max) => Math.max(min, Math.min(max, value));

// Keep font sizes and plotted geometry proportional as the modal changes width.
// The viewBox width follows the real SVG's CSS aspect ratio (height stays 285).
export function measureCanvas(id) {
    const svg = document.getElementById(id);
    if (!svg) return 640;
    const bounds = svg.getBoundingClientRect();
    if (!bounds.height || !bounds.width) return 640;
    return Math.max(640, Math.round(bounds.width / bounds.height * 285));
}

// Dashboard-only sizing: scale the viewBox to the actual plot rectangle.
// Unlike the modal explorer, there is no 640px minimum width for narrow cards.
export function measureDashboardDimensions(id) {
    const svg = document.getElementById(id);
    const plot = svg?.closest('.monitor-chart-plot');
    if (!plot) return null;
    const width = plot.clientWidth, height = plot.clientHeight;
    if (width < 20 || height < 20) return null;
    return { width, height, viewBoxWidth: Math.max(220, Math.min(3600, Math.round(width / height * 285))) };
}

export function detach(id) {
    const entry = attached.get(id);
    if (!entry) return;
    entry.abort.abort();
    if (entry.tooltip) entry.tooltip.hidden = true;
    if (entry.cursor) entry.cursor.style.display = 'none';
    if (entry.selection) entry.selection.style.display = 'none';
    attached.delete(id);
}

export function attach(id, rows, measures, dotnet) {
    detach(id);
    const svg = document.getElementById(id);
    if (!svg || !rows?.length) return;
    const plot = svg.closest('.monitor-chart-plot');
    const modal = svg.closest('.monitor-chart-modal');
    if (!plot || !modal) return;
    const tooltip = plot.querySelector('.monitor-chart-tooltip');
    const cursor = svg.querySelector('.monitor-chart-cursor');
    const selection = svg.querySelector('.monitor-chart-selection');
    if (!tooltip || !cursor || !selection) return;

    const abort = new AbortController();
    const opts = { signal: abort.signal };
    const type = svg.dataset.chartType;
    const bar = type === 'bar' || type === 'stacked';
    const min = rows[0].axis, max = rows[rows.length - 1].axis;
    const viewWidth = svg.viewBox?.baseVal?.width || 640;
    const left = 65;
    const plotWidth = viewWidth - 90;
    let dragging = false;
    let starting = null;
    let lastWheel = 0;

    tooltip.hidden = true;
    cursor.style.display = 'none';
    selection.style.display = 'none';

    // Unlike bounding-client-rect interpolation, an inverse SVG matrix also
    // works when the enlarged SVG is letterboxed by preserveAspectRatio.
    const fraction = evt => {
        const matrix = svg.getScreenCTM();
        if (matrix && typeof DOMPoint !== 'undefined') {
            const pt = new DOMPoint(evt.clientX, evt.clientY).matrixTransform(matrix.inverse());
            return clamp((pt.x - left) / plotWidth, 0, 1);
        }
        const bounds = svg.getBoundingClientRect();
        return clamp(((evt.clientX - bounds.left) / bounds.width * viewWidth - left) / plotWidth, 0, 1);
    };
    const nearestIndex = frac => {
        if (bar) return clamp(Math.floor(frac * rows.length), 0, rows.length - 1);
        if (min === max) return 0;
        const target = min + (max - min) * frac;
        let best = 0, distance = Infinity;
        for (let i = 0; i < rows.length; i++) {
            const delta = Math.abs(rows[i].axis - target);
            if (delta < distance) { distance = delta; best = i; }
        }
        return best;
    };
    const renderSelection = (a, b) => {
        selection.style.display = '';
        selection.setAttribute('x', String(left + Math.min(a, b) * plotWidth));
        selection.setAttribute('width', String(Math.abs(a - b) * plotWidth));
    };
    const hideHover = () => {
        tooltip.hidden = true;
        cursor.style.display = 'none';
    };
    const move = evt => {
        const frac = fraction(evt);
        if (dragging) { renderSelection(starting, frac); return; }
        const index = nearestIndex(frac);
        const row = rows[index];
        const x = bar ? left + (index + .5) * plotWidth / rows.length
            : min === max ? left + plotWidth / 2 : left + (row.axis - min) / (max - min) * plotWidth;
        cursor.setAttribute('x1', String(x));
        cursor.setAttribute('x2', String(x));
        cursor.style.display = '';
        tooltip.replaceChildren();
        const heading = document.createElement('strong');
        heading.textContent = row.label;
        tooltip.append(heading);
        row.values.forEach((value, i) => {
            const line = document.createElement('div');
            const name = document.createElement('span');
            name.textContent = measures[i] + ': ';
            const number = document.createElement('b');
            number.textContent = value == null ? '—'
                : new Intl.NumberFormat('fa-IR', { maximumFractionDigits: 2 }).format(value);
            line.append(name, number);
            tooltip.append(line);
        });
        tooltip.hidden = false;

        const parent = plot.getBoundingClientRect();
        const width = tooltip.offsetWidth;
        const height = tooltip.offsetHeight;
        const pointerX = evt.clientX - parent.left;
        const pointerY = evt.clientY - parent.top;
        const xMin = width / 2 + 8;
        tooltip.style.left = clamp(pointerX, xMin, Math.max(xMin, parent.width - xMin)) + 'px';
        // Flip below the pointer when there isn't sufficient room above it.
        // This avoids clipping at the chart's top edge (including tall tooltips).
        const fitsAbove = pointerY >= height + 18;
        tooltip.classList.toggle('is-below', !fitsAbove);
        tooltip.style.top = fitsAbove
            ? Math.min(pointerY - 9, parent.height - 9) + 'px'
            : clamp(pointerY + 15, 8, Math.max(8, parent.height - height - 8)) + 'px';
    };

    svg.addEventListener('pointermove', move, opts);
    svg.addEventListener('pointerleave', () => { if (!dragging) hideHover(); }, opts);
    svg.addEventListener('pointerdown', evt => {
        if (evt.button !== 0) return;
        starting = fraction(evt);
        dragging = true;
        hideHover();
        svg.setPointerCapture(evt.pointerId);
        renderSelection(starting, starting);
        evt.preventDefault();
    }, opts);
    svg.addEventListener('pointerup', evt => {
        if (!dragging) return;
        const ending = fraction(evt);
        dragging = false;
        selection.style.display = 'none';
        if (svg.hasPointerCapture(evt.pointerId)) svg.releasePointerCapture(evt.pointerId);
        if (Math.abs(ending - starting) >= .025) {
            dotnet.invokeMethodAsync('OnChartRangeSelected', Math.min(starting, ending), Math.max(starting, ending))
                .catch(() => {});
        }
        starting = null;
    }, opts);
    svg.addEventListener('pointercancel', () => {
        dragging = false; starting = null; selection.style.display = 'none';
    }, opts);
    svg.addEventListener('dblclick', evt => {
        evt.preventDefault();
        evt.stopPropagation();
        hideHover();
        dotnet.invokeMethodAsync('OnChartDoubleClick').catch(() => {});
    }, opts);

    // Capture before Chrome's default browser zoom. Intercept Ctrl+wheel over
    // the entire open report modal, not merely the SVG. The same gesture zooms
    // only the X-domain of the chart (around the cursor, or center elsewhere).
    window.addEventListener('wheel', evt => {
        if (!evt.ctrlKey || !modal.isConnected) return;
        evt.preventDefault();
        evt.stopImmediatePropagation();
        if (Math.abs(evt.deltaY) < .01) return;
        const now = performance.now();
        if (now - lastWheel < 60) return; // avoid excessive .NET roundtrips
        lastWheel = now;
        const anchor = svg.contains(evt.target) ? fraction(evt) : .5;
        const factor = evt.deltaY < 0 ? .78 : 1.28;
        dotnet.invokeMethodAsync('OnChartWheelZoom', anchor, factor).catch(() => {});
    }, { signal: abort.signal, capture: true, passive: false });

    attached.set(id, { abort, tooltip, cursor, selection });
}


// Non-Cartesian charts do not have an X-range to zoom. Still prevent Chrome's
// browser/page zoom while their expanded modal is open. This handler has the
// same attach/detach lifecycle as the Cartesian interaction implementation.
export function attachRadial(id) {
    detach(id);
    const root = document.getElementById(id);
    const modal = root?.closest('.monitor-chart-modal');
    if (!root || !modal) return;
    const abort = new AbortController();
    window.addEventListener('wheel', evt => {
        if (!evt.ctrlKey || !modal.isConnected) return;
        evt.preventDefault();
        evt.stopImmediatePropagation();
    }, { signal: abort.signal, capture: true, passive: false });
    attached.set(id, { abort });
}
