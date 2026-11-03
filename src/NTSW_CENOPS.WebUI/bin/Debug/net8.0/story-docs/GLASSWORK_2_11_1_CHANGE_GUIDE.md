# Glasswork 2.11.1 — Report Management UX & Safety

Based on the Glasswork 2.11 full project. No EF migration required.

## The eight requested changes
1. Removed the unused Contacts and HealthApis/HealthApisOld mock pages and corresponding menu/quick-navigation links. Existing database entities and permissions are intentionally retained to avoid breaking migrations and shared code.
2. `/reports` now uses the same `administration-page` / `administration-table` design pattern as `/settings`, with shared `DataTableToolbar` and `DataTable`, native filtering, sorting, pagination and row selection. Removed bespoke report table and search bar.
3. `DataTableToolbar<T>` now accepts three optional parameters: `ShowNewButton`, `ShowEditButton`, `ShowDeleteButton` (all default `true`). Hiding any of them never affects refresh, filtering or custom toolbar content.
4. Reports passes `false` for all three default actions, and supplies navigation-based `جدید` and `ویرایش` plus confirmation-based `حذف` through the existing `ToolbarContent` slot. User selects a row to enable Edit/Delete. `جدید` goes to `/reports/new`, `ویرایش` goes to `/reports/edit/{id}`.
5. Server-side `AutomatedJobService.DeleteAsync()` checks `MonitoringReports` for references, rejecting soft deletion with a Persian error when any dependent report exists. Deletion works again after associated reports have been deleted or reassigned.
6. Escape key now closes chat using the *existing* Blazor close-button handler, even when the message editor has focus, while suppressing background keyboard shortcuts. Implemented in the existing capture listener in `wwwroot/js/site.js`.
7. Chart cards remain actual accessible native radio inputs inside their labels, but radio circles are now visually hidden. Keyboard focus is highlighted around the card; card clicks activate the correct input.
8. Fixed report summary's two-column RTL grid: equal flexible columns, consistent right-aligned labels and left-aligned values, including LTR comparison keys.

## Developer notes
- All default toolbar flags are backward compatible.
- Report CRUD permissions (`reports.view`/`reports.manage`) stay unchanged.
- No schema modifications and no migrations are necessary.
- This version intentionally does not introduce report chart rendering or change job collection.
- No .NET SDK is installed in the working environment; build and browser smoke tests need to be performed locally.

## Quick local test plan
1. Open `/reports`: confirm existing reports appear in the shared DataTable; check filter, refresh, sort, page-size and pagination.
2. Click New, complete wizard, then return and select a report row. Confirm Edit opens that report.
3. With no row selected, Edit/Delete are disabled. With a selected row, Delete opens confirmation and refreshes the table on success.
4. Open `/settings` and other existing DataTables: confirm default New/Edit/Delete buttons remain visible and functional.
5. Create a report linked to a job, then attempt to delete that job. Expect rejection with a Persian message; delete the report, then try again.
6. Open chat and press Escape from an input and from elsewhere; chat closes.
7. Open new/edit report; click each entire chart card to change selection; tab with keyboard and use Space.
8. Check the right-to-left report summary: values align consistently on the left, including the comparison key.
9. Confirm `/contacts`, `/health-apis`, `/health-apis-old` are no longer routable and no longer appear in sidebar/quick navigation.

### Note about archives
Full archive contains the complete runnable solution source. Changed archive contains modified/new files only and a `DELETED_FILES.txt` listing removed files (remove these from your existing project when applying manually). A unified diff is also provided.
