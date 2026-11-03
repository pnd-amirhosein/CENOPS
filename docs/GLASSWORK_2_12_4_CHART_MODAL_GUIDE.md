# Glasswork 2.12.4 — Report Explorer refinement

Based on **Glasswork 2.12.3**. This release adds no database migration.

## Changes

1. **Wider report viewer:** modal maximum width 1440 px (within viewport), tighter vertical padding, compact heading/toolbar/legend.
2. **Smaller axes:** SVG axis labels use approximately 9.5 px instead of 12 px. The expanded view uses about 9.7 px.
3. **No default desktop scroll:** a 16:9 landscape viewport receives a fixed-height chart and no modal-body scrollbar; genuinely short/narrow screens may scroll to retain access to controls.
4. **Readable hover:** pointer tooltips automatically flip below the pointer when near the top edge; position is clamped horizontally, and the expanded chart has visible overflow.
5. **Ctrl + mouse wheel:** while the report modal is open, Chrome browser zoom is prevented. Scrolling with Ctrl adjusts the X-axis visible range around the cursor; ordinary scrolling is unchanged. Wheel events are attached with `passive: false` and disposed on component removal. Interaction uses the SVG matrix for accurate pointer coordinates when letterboxed.
6. **Single-line guide label:** `خطوط راهنما` uses nowrap.
7. **Zoom and reset:** double-click inside the expanded chart resets **only zoom**; `↺ Reset` restores all loaded-data range selections, custom dates, hidden/visible series, gridlines, and zoom.
8. **Reusable modal Escape policy:** `AppModal` now exposes `CloseOnEscape` (`true` by default). The app routes Escape only to the topmost modal. An explicit `false` consumes Escape without closing. This does **not** alter the `CloseOnBackdrop` policy or the Close button.
9. **Report modal:** sets `CloseOnEscape="false"` and removes its old chart-specific Escape close handler. Close and backdrop behavior are unchanged.
10. **Extra chart types:** `smooth` (cubic curved line), `lollipop` (stem and marker), and `stackedarea` (positive/negative stacked areas). All appear in report creation, have backend validation, and are rendered by the shared chart component.

## Changed source files

- `src/HealthMonitoringApp.WebUI/Components/Common/AppModal.razor`
- `src/HealthMonitoringApp.WebUI/Components/Common/AppModal.razor.css`
- `src/HealthMonitoringApp.WebUI/Components/Pages/MonitoringReportChart.razor`
- `src/HealthMonitoringApp.WebUI/Components/Pages/MonitoringReportEditor.razor`
- `src/HealthMonitoringApp.WebUI/Components/App.razor` (cache-busting CSS and JS versions)
- `src/HealthMonitoringApp.WebUI/wwwroot/css/monitoring-reports.css`
- `src/HealthMonitoringApp.WebUI/wwwroot/js/monitor-chart-interactions.js`
- `src/HealthMonitoringApp.WebUI/wwwroot/js/site.js`
- `src/HealthMonitoringApp.Infrastructure/Reports/MonitoringReportService.cs`
- `src/HealthMonitoringApp.Infrastructure/Reports/MonitoringReportChartService.cs`

## Tests performed

- `node --check` on both changed JavaScript files: passed.
- Headless Chromium modal sizing smoke test: at 1365x768, 1920x1080 and 1200x675, no modal/body overflow and the guide-lines label remained on one line. A narrow 800x600 layout also displayed correctly in the static fixture.
- Headless Chromium behavior smoke test: Ctrl+wheel prevented browser default and invoked `OnChartWheelZoom`; double-click invoked `OnChartDoubleClick`; Escape was prevented when the modal was configured with `CloseOnEscape=false`.
- Source checks: chart types match in wizard, backend allowlist, and renderer; no SVG `<text>` attributes; date inputs retain explicit `@onchange` handlers.
- Both ZIP archives verified by `zipfile.testzip()`.

**Build limitation:** the .NET SDK is not installed in this environment, so `dotnet build` and full interactive Blazor tests were not run. Please compile locally before deployment.

## Manual test checklist

- At 1366x768, expand a report with 4+ series; check modal width, no vertical modal scroll, compact X/Y labels, and `خطوط راهنما` label.
- Hover near top and side chart edges. Tooltip must stay visible and its values must match the hovered point.
- Drag across the chart to select a region; Ctrl+wheel up/down to zoom in/out without changing Chrome zoom.
- Double-click: should reset zoom **without** changing filters or hidden series.
- Toggle series off, disable grid, change date range, zoom, then click `↺ Reset`: all should return to default.
- Press Escape in the report viewer: nothing should close. Click Close or backdrop: should close.
- Test Escape on other `AppModal` instances (e.g. confirmation modal): default remains enabled.
- Create a report of each new chart type, save, load gallery and expand.
- Verify no regression on existing line, bar, scatter, area, step and stacked charts.
