# Glasswork 2.12.2 — Report Gallery & Chart Explorer

Based on **Glasswork 2.12.1**. No database migration.

## Changes

1. Sidebar link matching now uses `NavLinkMatch.All` for exact active-state matching. `/reports` no longer highlights together with `/reports/manage`.
2. Deleted the text `مدیریت گزارش‌های ساخته‌شده از داده‌های ذخیره‌شده کارهای خودکار` from the reports management header.
3. Removed the old filled/outlined descriptive heading box from the `/reports` gallery (including its text).
4. Added a live client-side report search control and a management-navigation button on the same toolbar row. Search matches report name, source job and comparison field.
5. The management button appears only for users with `reports.manage`; the gallery remains protected by `reports.view`. This is additional UI gating, not a replacement for backend authorization.
6. Added Area (`area`), Step (`step`), and Stacked Bar (`stacked`) chart types to the report wizard, the report service's allowed types, and the shared SVG renderer. Existing Line, Bar, and Scatter remain supported.
7. Added an expand control next to each chart type badge. It opens the project's reusable `AppModal` with an enlarged copy of the *same* SVG chart component. Features:
   - Data timeframe presets, including last 24h / 7d / 14d measured against the most recent stored point, or recent point counts for numeric X axes.
   - Custom from/to dates when X is a timestamp.
   - Per-measure series visibility; at least one measure stays selected.
   - Toggle horizontal gridlines.
   - Crosshair and detailed hover readout.
   - Mouse drag to zoom a selected range, then reset the zoom.
   - Escape key and modal close control.
   - The color for each series remains stable when other series are hidden.

## Important scope and data notes

- The explorer reads already-collected `AutomatedJobData` through `MonitoringReportChartService`. It NEVER executes production SQL.
- For performance, the existing gallery loader reads **up to the 14 newest daily records per source job**. Its custom date filter can only filter data **already loaded into the gallery**; it does not fetch older history on demand. Larger/full-history date browsing should be an explicit future backend feature.
- The existing source projection bounds chart points (normally 90 points, or 32 for grouped/stacked columns); dragging zooms the projected data, it does not restore raw points that were downsampled for display.
- Duplicate X values still use the most recently collected record, rather than being summed.
- JS interaction module `wwwroot/js/monitor-chart-interactions.js` is loaded dynamically only when an enlarged chart is shown. Hover is handled locally in JS, while completed zoom ranges are passed to Blazor.
- No report schema, job scheduler, job data storage or database migration changes.

## Changed/new source

- `src/HealthMonitoringApp.WebUI/Components/SideBar/SidebarLink.razor`
- `src/HealthMonitoringApp.WebUI/Components/Pages/MonitoringReports.razor`
- `src/HealthMonitoringApp.WebUI/Components/Pages/MonitoringReportGallery.razor`
- `src/HealthMonitoringApp.WebUI/Components/Pages/MonitoringReportEditor.razor`
- `src/HealthMonitoringApp.WebUI/Components/Pages/MonitoringReportChart.razor`
- `src/HealthMonitoringApp.WebUI/wwwroot/css/monitoring-reports.css`
- `src/HealthMonitoringApp.WebUI/wwwroot/js/monitor-chart-interactions.js` **(new)**
- `src/HealthMonitoringApp.Infrastructure/Reports/MonitoringReportService.cs`
- `src/HealthMonitoringApp.Infrastructure/Reports/MonitoringReportChartService.cs`

## Manual test checklist

1. Navigate to `/reports`, then `/reports/manage`: only one sidebar child should be highlighted at a time. Check other parent navs after the exact-match change.
2. Open management: its former subtitle is absent; existing DataTable still works.
3. Open gallery: search by report title/job name; clear search; check empty state.
4. Sign in with a user who has `reports.view` but not `reports.manage`: they can see charts but NOT the management button. Verify direct URL and backend actions remain permission-checked.
5. Create one report for each of six chart types and verify the chart draws correctly, including stacked positive/negative measurements and null gaps.
6. Expand a chart and try preset/custom dates, series visibility, gridlines, crosshair hover and drag zoom. Reset zoom; close via Escape and via the button.
7. Check dark theme, small screens, Blazor reconnection, and cases with no data.

## Validation limits

Source checks, JS syntax checks, and ZIP archive integrity were performed. The local environment did not have the .NET SDK or a running SQL Server/Blazor browser to compile and run full integration/UI tests; please compile and test the solution locally before deployment.
