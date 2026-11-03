# Glasswork 2.17 — Dashboard Content Experience Refinement

**Baseline:** `NTSW_CENOPS_Glasswork_2_16.zip`.

**Scope:** Card-library presentation, small content previews in the picker, compact dashboard layout and the default-dashboard star. No database schema changes, no changes to dashboard JSON format, and no authorization/role logic changes.

## 1. Picker: content-specific experiences, consistent card chrome

All six types use the **same rounded card shape, neutral outer border and type-colored right border**. Previous top/dashed/bottom edges and background-gradient variants have been removed. Types are differentiated by **information architecture and dimensions**, not mismatched outlines.

When **همه موارد** is active, each type is shown in its own group with a small subdued label, a count, and a faint divider. Switching to a single category omits the redundant group heading. Group order: Notes, Announcements, Todos, SQL, cURL, Reports.

| Type | Picker appearance |
| --- | --- |
| Notes | Existing compact card size and title/excerpt arrangement are retained, with the standardized right accent. |
| Announcements | Two per row on roomy viewports; shorter cards, compact vertical spacing, stronger one-line headline, two-line excerpt. |
| Todos | Original compact card width, taller card; first three real task items, including completion-state indicators. Each item is **one physical line**, trimmed with `text-overflow: ellipsis`; no line-wrap. |
| SQL | Two wider cards per row; tall card with up to two description lines and a three-line monospaced preview of the actual SQL value. |
| cURL | Same wider/taller two-column structure as SQL, with description and the actual request beginning in the monospaced preview. |
| Reports | Existing compact report card remains unchanged apart from the standardized right accent. |

The two-column layouts gracefully switch to one column on narrow screens so titles and snippets stay readable.

### Data delivery, security and performance

- `DashboardContentItem` now has two optional read-only presentation fields: `CodePreview` and `TodoItems`. They do **not** alter persisted dashboard references (`type`, `id`).
- SQL and cURL keep description separate from the existing bounded code prefix; their preview now presents **both** instead of choosing only one.
- Todo items are retrieved from the existing `TodoItems` table, **batched across the returned todo IDs**, then reduced to the first three in position order. The database returns a bounded prefix of each item text. This avoids a separate query per card.
- All of this runs within `DashboardContentService`, after existing access and visibility checks. Deleted source items are still excluded; card detail previews remain permission-aware.
- The detail-preview footer no longer duplicates **بستن پیش‌نمایش**; its existing top-corner X continues to close the detail view. Its selection action remains available.

## 2. Dashboard itself

- Removed `GLOBAL / SHARED` and `PERSONAL / MY WORKSPACE` overlines above zone headings.
- Replaced the two-row dashboard grid with a content-height column. **Neither global nor personal sections reserve vertical space** for nonexistent cards. Empty sections keep their original guidance message in a much shorter area; populated sections grow to exactly the height their tiles require.
- Reduced topbar / heading / section spacing and removed the tile-grid bottom gutter.
- The existing default-dashboard action is now a **golden star icon directly on the right side of the RTL dashboard selector**. No redundant text label is rendered; accessible title and label describe the action, and the current default uses a filled gold star. Underlying `MakeDefaultAsync`/`SetDefaultAsync` behavior is unchanged, including its disabled state for already-default or missing personal dashboards.

## 3. Gauge-chart terminology

Changed the Persian display term `گیج` to **`عقربه‌ای`** in chart-type lists and legends. The internal chart type key `gauge`, chart rendering, and saved reports remain untouched. The historic chart-types guide now uses the matching Persian term.

## 4. Source files changed

- `src/HealthMonitoringApp.Application/Dashboards/DashboardContentModels.cs`
- `src/HealthMonitoringApp.Infrastructure/Dashboards/DashboardContentService.cs`
- `src/HealthMonitoringApp.WebUI/Components/Pages/DashboardContentPicker.razor`
- `src/HealthMonitoringApp.WebUI/Components/Pages/DashboardContentPicker.razor.css`
- `src/HealthMonitoringApp.WebUI/Components/Pages/Home.razor`
- `src/HealthMonitoringApp.WebUI/Components/Pages/Home.razor.css`
- `src/HealthMonitoringApp.WebUI/Components/Pages/MonitoringReportEditor.razor`
- `src/HealthMonitoringApp.WebUI/Components/Pages/MonitoringReportChart.razor`
- `GLASSWORK_2_12_5_CHART_TYPES_GUIDE.md` (terminology)
- `GLASSWORK_2_17_DASHBOARD_EXPERIENCE_GUIDE.md` (this guide)

## 5. Manual test checklist

1. Run `dotnet restore` / `dotnet build NTSW_CENOPS.sln` against the project SDK and usual dependencies.
2. Dashboard → Edit → Add content → **همه موارد**: verify muted sections appear in the correct order; switch category and verify its redundant section header disappears. Verify no card has a top, dashed or bottom type accent.
3. Check several announcements in a broad viewport: exactly two across, compact height, visually strong single-line titles.
4. Create a todo with five tasks, including completed tasks and very long text. Confirm first three items appear, remain one line each, and truncate. Click its eye button to check all tasks still display in the full preview.
5. For SQL and cURL containing multiline code and a long description, check 2 description lines, approximately 3 code-preview lines and two cards across. Verify eye preview still returns full code; verify sensitive/private items remain permission-filtered.
6. Confirm notes and reports remain selectable, previewable, and correctly colored. Select multiple cards, test close confirmation, and Save/Cancel.
7. With no global/personal tiles, inspect the dashboard: compact guidance without reserved blank rows. Add several cards to one zone and verify its natural height; the other zone should still be compact.
8. Toggle between personal dashboards, set a new default with the golden star beside the selector, refresh and confirm persistence. Confirm an already-default star is filled/disabled.
9. Create/open a gauge-type report. Verify Persian UI reads **عقربه‌ای**; the chart still uses the saved `gauge` type.
10. Retest narrow screens and RTL text, specifically code clipping, todo ellipsis and two-column collapse.

## 6. Verification in this environment

- Confirmed modifications are sourced from the **2.16** ZIP, not from older project snapshots.
- CSS syntax and targeted structural/source invariants were validated locally; archive integrity is validated at packaging time.
- **Not verified here:** a .NET build, EF Core SQL translation against the actual database, real Blazor event behavior, or live viewport rendering. The .NET SDK/SQL Server are not installed in this runtime; these require local integration testing.

**Migration required:** None.
