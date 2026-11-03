# Glasswork 2.20 — Dashboard + Wiki editor refinements

**Baseline:** `NTSW_CENOPS_Glasswork_2_19.zip`  
**Scope:** Requested UX/behavior refinements only. No schema migration or new dependencies.

## 1. Dashboard

### Announcements

The announcement dashboard tile now has the shared white surface, subtle border, shadow, and original category-colored **right border**. The message itself remains an oversized, centered heading; the label/author remain colored. As before, the dashboard announcement is not clickable and does not open a preview.

### Swapping cards

`wwwroot/js/dashboard-grid.js` now exchanges the **complete occupied rectangles** of the two cards on a center drop. For a card A at `{x:0,y:0,w:120,h:120}` and card B at `{x:200,y:0,w:300,h:90}`, swapping produces:

- A at `{x:200,y:0,w:300,h:90}`
- B at `{x:0,y:0,w:120,h:120}`

The content stays with its respective identity; geometry follows the destination slot. This is distinct from edge and empty-space drops, which retain the original geometry. Any other intersecting cards continue to use the established collision cascade. Save persists the resulting `x/y/pw/ph/cw` values; Cancel restores the pre-edit geometry.

### Author & preview actions

For dashboard notes, todos, SQL, and cURL cards, the upper-right line contains **`نوع محتوا - نویسنده`**, and the **مشاهده محتوا** action sits at the opposite (left) end of that line. The old author footer is removed. The announcement remains a special centered headline. Report charts retain their original chart UI, plus their existing preview affordance.

## 2. Content library / picker

### Chart-type identification

`DashboardContentItem.ChartType` is a new, optional display value. The `DashboardContentService` report projection reads `MonitoringReport.ChartType` in the existing query and carries it to the picker. `DashboardChartLabels.Get()` converts the technical value to the Persian chart name (e.g. `bar → ستونی`, `gauge → عقربه‌ای`). The displayed chart type is separate from the report title, so similarly named reports remain distinguishable.

### Shared byline layout

All picker cards show their type and author together in the top-right line, with **مشاهده محتوا** in the far-left action. The selection number/plus sits beside the type line. The author footer and the associated empty space have been removed. All card types retain the consistent right-colored-border design.

### Preselection, locking, and synchronization

The picker receives two lists from the editor:

- `PersonalItems`: cards in the currently selected personal dashboard (including unsaved changes staged in this edit session).
- `GlobalItems`: cards in the shared global dashboard (including unsaved changes staged in this edit session).

On open, the picker resolves the editable personal layout through the permission-aware catalog and preselects those cards, even if the initial search page does not display them. The user can toggle existing personal cards off, toggle them back on, and add more cards. **Apply selections** synchronizes the personal layout while retaining the existing placement/size for unchanged cards. The user must still press the dashboard **Save** button to persist.

Any card already in the global dashboard is shown as **selected and locked**. It cannot be selected/deselected in the picker for either zone, even by Admin/Owner; its separate preview action still works. Global removal remains a deliberate dashboard-editor action with confirmation.

The global add action remains Admin/Owner-only, checks content visibility and adds selected non-global cards. It does not replace the global layout. The unsaved-selection warning is triggered by **actual changes** compared with the picker's initial selection set, not merely by opening a picker containing previously selected cards.

**Important:** If initial selection lookup fails, the picker disables actions that could overwrite an unknown selection state and shows an error. This is safer than treating a failed lookup as an empty dashboard.

## 3. Responsive report tiles

`MonitoringReportChart` now accepts `DashboardMode` (default `false`). This preserves every existing non-dashboard use of the chart renderer. For dashboard charts:

- The plot takes the remaining vertical space inside the tile without scroll cropping.
- For Cartesian SVG charts, `measureDashboardCanvas` reports the resized plot's actual width/height. The component updates its SVG viewBox width to match the available aspect ratio, rather than displaying a small fixed-width chart inside a wide tile.
- The header, legend and footer compact progressively for shorter cards; optional footer details can be hidden at short heights while the primary plotted data remains visible.
- The existing pie/doughnut/treemap/spider/gauge renderer retains its geometry and scales into its allotted space without stretching circles into ellipses.
- Resizing report tiles now has a **165px minimum height** to protect the chart from unusably short dimensions, while the canvas retains its fine pixel positioning for other card types.

The standalone report gallery/expanded modal are unchanged.

## 4. Wiki editor shortcuts

A lightweight scoped listener, `wwwroot/js/wiki-editor-shortcuts.js`, is attached to each relevant editor root. The handler intercepts only the specified shortcuts when focus is in an editable field, not keystrokes in the rest of the site. It is detached on component disposal.

### Todo create/edit

| Shortcut | Result |
| --- | --- |
| `↓` | Focus next task text. On last task, insert a task below and focus it. |
| `↑` | Focus previous task text; do nothing on first. |
| `Shift + ↑` | Insert a task before the current one and focus it. |
| `Shift + ↓` | Insert a task after the current one and focus it. |
| `Shift + Space` | Toggle checkbox in the current task. |
| `Shift + Enter` | Validate and open existing **save confirmation** modal. Does not save silently. |

Keyboard insertions reuse the existing todo data model and 120-item limit. New items receive normal keys. Focus is restored after Blazor rerenders. The note/todo drag-and-drop functionality remains independent and intact.

### Note create/edit

`Shift + Enter` validates and opens the note's existing save confirmation modal. It also works from regular text and code editing fields. No save occurs until the confirmation button is pressed.

## 5. Files changed

- `src/HealthMonitoringApp.Application/Dashboards/DashboardContentModels.cs`
- `src/HealthMonitoringApp.Application/Dashboards/DashboardChartLabels.cs` **(new)**
- `src/HealthMonitoringApp.Infrastructure/Dashboards/DashboardContentService.cs`
- `src/HealthMonitoringApp.WebUI/Components/App.razor`
- `src/HealthMonitoringApp.WebUI/Components/Pages/Home.razor`
- `src/HealthMonitoringApp.WebUI/Components/Pages/DashboardContentPicker.razor`
- `src/HealthMonitoringApp.WebUI/Components/Pages/DashboardContentPicker.razor.css`
- `src/HealthMonitoringApp.WebUI/Components/Pages/DashboardTile.razor`
- `src/HealthMonitoringApp.WebUI/Components/Pages/DashboardTile.razor.css`
- `src/HealthMonitoringApp.WebUI/Components/Pages/MonitoringReportChart.razor`
- `src/HealthMonitoringApp.WebUI/Components/Tasks/TodoEditor.razor`
- `src/HealthMonitoringApp.WebUI/Components/Notes/NoteEditor.razor`
- `src/HealthMonitoringApp.WebUI/wwwroot/js/dashboard-grid.js`
- `src/HealthMonitoringApp.WebUI/wwwroot/js/monitor-chart-interactions.js`
- `src/HealthMonitoringApp.WebUI/wwwroot/js/wiki-editor-shortcuts.js` **(new)**
- This document **(new)**

## 6. Local validation

A real compile/SQL-backed test could not be run in the editing environment because the .NET SDK and SQL Server were not available. Perform these checks locally:

1. Build the solution (`dotnet build`); fix any environment-specific errors before deploying.
2. Open a dashboard in edit mode. Swap a 120×120 card and a 300×90 card; confirm that **both** dimensions and positions exchange, then Save, refresh, and Cancel in a separate edit.
3. Open the picker with existing personal and global items. Confirm personal selection indices, deselect/reselect, the locked global badge, disabled global selection, working previews, and staged Save/Cancel behavior. Test as Admin, Owner, regular owner-of-dashboard, and read-only viewer.
4. Search the report category with multiple same-title reports of different chart types; verify their labels.
5. Resize line/bar/radial report cards to near the minimum height and across narrow/wide widths; check chart labels, legend visibility, and that plot data is never clipped in the tile. Test the expanded report modal separately for regressions.
6. Use every shortcut with focus in todo text, checkbox, and note paragraph/code/title; confirm focus movement, item insertion order, checkbox state, save confirmation, error state for missing titles, and the 120-task limit.
7. Test both right-to-left and left-to-right todo text, and keyboard shortcut behavior after dragging/reordering todo rows.

### Verification performed while packaging

- Chromium browser interaction checks for pixel resize, move, center swap **including dimensions**, edge drops, collision behavior and mobile layout.
- Chromium keyboard-handler smoke test for navigation, checkbox toggle, insertion/focus, confirmation event, and note shortcut.
- Static CSS and JavaScript parsing plus a short-report-card layout smoke test.
- ZIP archive integrity validation.

**Database:** No migration required; chart types are read from existing report records, and dashboard geometry remains in the established layout JSON.
