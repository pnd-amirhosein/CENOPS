# Glasswork 2.12.5 — New Report Chart Types & Explorer Fixes

Based on Glasswork 2.12.4. No database migration.

## Added chart types

The report creation wizard and server-side allowlist support:

| Internal name | Persian label | How data is represented |
| --- | --- | --- |
| `pie` | دایره‌ای | Positive values of the FIRST numeric measure, slices keyed by the comparison field. |
| `doughnut` | حلقه‌ای | Same shares as pie, shown as a ring. |
| `treemap` | نقشه درختی | Areas proportional to positive values of the first measure; binary partition layout. |
| `spider` | عنکبوتی | Comparison field as categorical radial axes; each numeric measure a series. Needs >=3 points, no null/negative values; up to 12 sampled axes for readability. |
| `bubble` | حبابی | Comparison key as X, first measure as Y, absolute value of the second measure as bubble-size (first measure if only one is present). |
| `gauge` | عقربه‌ای | Latest value of each measure relative to its own observed maximum in the currently loaded range; NOT an operational target or SLA. |

`spiral` is intentionally omitted: an honest spiral chart needs a *cycle/period* definition (hour of day, weekday, month, etc.), which the current one-comparison-field/numeric-measures contract does not provide. Adding one now would risk suggesting meaningless periodic patterns.

Pie/doughnut/treemap ignore non-positive values; when nothing positive remains, they display a clear empty state. Categories beyond the first 15 largest slices are grouped under "سایر" so labels remain readable. All charts use the existing persisted report data (no SQL re-execution).

## Interaction and layout fixes

- Fixed Ctrl+wheel zoom-out getting stuck after maximum zoom: the zoom service now computes ranges from `_zoomMin` / `_zoomMax`, not visible chart points. Nearest data points keep the chart mounted even between samples.
- Cartesian SVG expanded view: JS measures the real on-screen aspect ratio and adjusts the viewBox width; the actual plot extends across the modal without stretching the labels, circles, or axes. Cursor selection and zoom work with the updated geometry.
- Reset is `بازنشانی`, with a consistent icon and 34px control height.
- Increased separation between the interactive series badges, help copy, and chart; removed the former 72px clipped-badge scroller on desktop.
- Expanded radial charts suppress Ctrl+wheel Chrome browser zoom within the open modal; the gesture doesn't change their axis (they have no conventional X-range).
- Kept the existing modal close behavior and permissions.

## Changed source files

- `src/HealthMonitoringApp.WebUI/Components/Pages/MonitoringReportChart.razor`
- `src/HealthMonitoringApp.WebUI/Components/Pages/MonitoringReportRadial.razor` (new)
- `src/HealthMonitoringApp.WebUI/Components/Pages/MonitoringReportEditor.razor`
- `src/HealthMonitoringApp.WebUI/wwwroot/css/monitoring-reports.css`
- `src/HealthMonitoringApp.WebUI/wwwroot/js/monitor-chart-interactions.js`
- `src/HealthMonitoringApp.Infrastructure/Reports/MonitoringReportService.cs`
- `src/HealthMonitoringApp.Infrastructure/Reports/MonitoringReportChartService.cs`

## Suggested local regression test

1. Build the solution and open `/reports/manage`, edit a report and verify all 15 chart choices.
2. Create a sample with positive values across several comparison keys; verify Pie, Doughnut, and Treemap show the first measure and the slice tooltip names/values.
3. Check Spider with three or more keys and several numeric measures; deliberately try null or negative values to confirm the compatibility message.
4. Check Bubble with one and two measures, verify its Y position and bubble-size description.
5. Check Gauge with several measures and a latest value below the observed maximum; verify `بیشینهٔ مشاهده‌شده` is visible and no target/SLA is implied.
6. Open the enlarged Cartesian chart on 1366×768 and 1920×1080; check full-width plot, axis font proportions, hover/cursor alignment and no overflowing badges.
7. Ctrl+wheel up until the chart is near a single point, then Ctrl+wheel down repeatedly; the complete range must return. Double-click should reset zoom, while `بازنشانی` resets range and series toggles too.
8. Close the enlarged view and verify browser Ctrl+wheel works normally again, with all JS handlers disposed. Escape still must not close the report modal.

## Validation limitations

Node JavaScript syntax and ZIP integrity checks were run here, with basic structural source assertions. This runtime has no .NET SDK; compile/run-time Blazor validation and SQL Server integration tests must be performed locally.
