# Glasswork 2.12.1 — Razor SVG text fix

Base: Glasswork 2.12. Only modified source file:
`src/HealthMonitoringApp.WebUI/Components/Pages/MonitoringReportChart.razor`

## Fixed

Two Razor compiler diagnostics RZ1023 on lines 30 and 36:

`<text>` is Razor's special templating tag and cannot have attributes. The SVG chart
had used `<text x="..." y="..." text-anchor="...">` to render its axis labels.

Both labels now use `@SvgTick(...)`, a `RenderFragment` which creates an actual
SVG `<text>` node using Blazor's `RenderTreeBuilder`. X/Y positioning, alignment,
CSS class and label contents are preserved; strings are added with `AddContent`
so they are treated as text rather than raw HTML.

## Deploy

- Full ZIP: replace the Glasswork 2.12 solution with the 2.12.1 solution.
- Changed ZIP: replace just the `MonitoringReportChart.razor` file.
- No database migration needed.

## Verification

- Verified both illegal SVG `<text ...>` declarations are gone.
- Verified the new render fragment creates SVG text with the original attributes.
- Validated full and changed ZIP integrity.
- A .NET compile was **not** run (SDK unavailable in this environment).
