// 26px matches SourceFileViewer's fixed virtualized row height.
export function jumpToLine(scroller, line) {
    if (!scroller) return;
    const row = 26;
    const target = Number.isFinite(Number(line)) ? Math.max(1, Number(line)) : 1;
    const viewport = scroller.clientHeight || 0;
    scroller.scrollTop = Math.max(0, (target - 1) * row - viewport / 2 + row / 2);
}
