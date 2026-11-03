# Glasswork 2.12 — Report chart gallery and Skyline quick-create

## Routes

- `/reports` — **گزارش‌ها**: chart cards based on *stored* AutomatedJobData. Three RTL columns on wider screens; two and one on narrower screens.
- `/reports/manage` — **مدیریت گزارش‌ها**: existing report DataTable, refresh/filter/selection, confirmation-based deletion.
- `/reports/new` — report wizard; no longer in the sidebar, but available via DataTable «جدید» and Skyline +.
- `/reports/edit/{id}` — existing report wizard edit route, accessible using DataTable «ویرایش».

The wizard redirects to `/reports/manage` after save. Its Back link also navigates to management.

## Toolbar reuse

`DataTableToolbar<T>` now has **two** optional projection slots:

- `LeadingToolbarContent` — immediately after Refresh, exactly where the built-in CRUD buttons appear in RTL.
- `ToolbarContent` — existing trailing slot, next to Filter, unchanged for all existing callers.

The management page hides built-in New/Edit/Delete with the previously implemented `ShowNewButton`, `ShowEditButton`, and `ShowDeleteButton` flags. It inserts custom buttons in `LeadingToolbarContent`, reproducing the built-in icons, labels, class names and selected-row disabled states. Existing users of the toolbar need no changes.

## Skyline shortcut

Ctrl+B (or Command+B) opens the + creation menu. **R** on an open menu navigates to `/reports/new` if the user has `reports.manage` access. The menu item uses the same server-side permission-checking navigation handler as Notes/cURL/SQL. Typing inside input/textarea does not trigger the shortcut. Chat's modal still has keyboard priority.

## Chart behavior

- Access permission: `reports.view` (same as reporting data and definitions).
- Uses saved report's `ComparisonField`, `MeasureFieldsJson`, and `ChartType` (`line`, `bar`, `scatter`). Does not run production SQL queries.
- Reads **the 14 most recent available day documents** per source job; documents are reused in-memory for reports using the same job.
- Flattens execution windows and parses comparison keys as numeric or datetime; measurements must be numeric or null (gaps).
- When the same comparison value appears more than once, the row with the newest `collectedAtUtc` is used. **Repeated comparison keys are NOT aggregated or summed.** Later versions could add sum/average aggregation settings if required.
- Up to 90 evenly sampled line/scatter points or 22 bar-group points per chart, spanning earliest through latest stored data. Full data stays in the database.
- Incompatible JSON rows are skipped and shown as a warning; no usable rows → an informative empty chart card. Missing data → a no-data card.
- Field names are dynamic: chart renderer does not assume any specific SQL column names.
- Charts are pure Blazor SVG without external JavaScript packages.

## Database

No new table or migration. The new chart service is registered via DI. All existing report/job entities remain unchanged.

## Manual test checklist

1. Navigate to sidebar → **سرویس مانیتورینگ**. Confirm **مدیریت گزارش‌ها** and **گزارش‌ها**, with no **گزارش جدید** entry.
2. Open management; confirm original-style New/Edit/Delete are beside Refresh on the right; Filter remains in its existing place. Select a report to enable Edit/Delete.
3. New and Edit open the existing wizard. Saving returns to management.
4. Open Skyline + or press Ctrl+B, then press R. Confirm `/reports/new` opens. Check absent/unauthorized menu item for an account without `reports.manage`.
5. Open `/reports`; confirm all saved reports render as three right-to-left chart tiles per wide row, wrapping responsively.
6. Test an Hour-based line report, a number-based scatter report, and a bar report. Verify names/series come from the saved report definition.
7. Test a report whose job has never saved data, a report with incompatible/changed columns, and an inactive job with previously collected data.
8. Verify browser console and database queries: chart rendering must not connect to production log SQL servers.

## Validation performed here

- `node --check` passed for `wwwroot/js/site.js`.
- Automated source/integration checks passed for routes, navigation, permissions, toolbar placement, data-service registration, static files, and chart shapes.
- Full and changed ZIP integrity checks passed.
- The environment does **not** have the .NET SDK or a live SQL Server/Blazor browser. Compile and test UI interactively in your development environment.
