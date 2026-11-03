# Glasswork 2.15 — Dashboard content library and borderless layout

Based on the user's latest `NTSW_CENOPS_Glasswork_2_14.zip` source. This release builds on the separate global/personal dashboard configuration foundation introduced in 2.14.

## What's new

- **Borderless dashboard zones.** Global (top) and personal (bottom) are flush in the workspace: no rounded zone boxes, card frame, shadow, artificial zone gap, or empty-state dashed container. The **individual content cards** remain distinct so they can be read and rearranged in later versions.
- **Add content in edit mode.** A dedicated `افزودن محتوا` button appears beside Save/Cancel and opens a wide, scrollable, responsive picker.
- **Searchable library.** Category rail contains All, Notes, Announcements, Todos, SQL, cURL, Reports. Search is server-side and includes titles, authors, body content of notes, descriptions AND SQL/cURL code, individual todo-item text, announcement text, and report fields/job metadata. The picker loads up to 30 recent results from each category in All mode or up to 180 for a single category. Search input is debounced 260 ms.
- **Multi-select.** Click a result to select/deselect it; a numeric badge shows its position in the selection. The preview button shows the total selected and opens a compact list where items can be removed. The modal has Cancel, Add to Personal, and (Admin/Owner only) Add to Global.
- **Save/Cancel flow.** Selecting Add stages the referenced cards in the corresponding workspace, leaving the picker and entering the editable dashboard preview. Dashboard Save persists them; dashboard Cancel restores saved layouts. Already-added references are deduplicated. Editing mode allows removing individual dashboard cards with ×.
- **Report cards.** When a referenced report has available chart data, the existing `MonitoringReportChart` component is rendered using the existing chart service. Other references render title/summary/author with a navigation link to the source item. Reports without chart data show the normal summary/link tile.
- **Workspace selection behavior preserved.** Switching personal dashboards changes only their associated bottom layout. The shared global layout and the user's default dashboard behavior remain independent.

## Data contract and database

No new database tables or migrations are needed. `LayoutJson` from the existing configurations now contains an ordered JSON array of source-item references, **not copies of source content**:

```json
[
  { "type": "note", "id": 13 },
  { "type": "todo", "id": 4 },
  { "type": "report", "id": 19 }
]
```

Supported `type` values: `note`, `announcement`, `todo`, `sql`, `curl`, `report`. `id` is a positive source table ID (`long` to support report IDs). 60 unique references maximum **per global/personal layout**. Old `[]` layouts continue to work. The service canonicalizes JSON when saving, validates type and ID, checks existence, and rejects duplicates/unknown fields/oversized layouts.

## Files changed

| File | Role |
| --- | --- |
| `src/HealthMonitoringApp.Application/Dashboards/DashboardContentModels.cs` | Reference/card/result DTOs and content-service contract. |
| `src/HealthMonitoringApp.Infrastructure/Dashboards/DashboardContentService.cs` | Permission-filtered catalog, full-body search and resolver. |
| `src/HealthMonitoringApp.Infrastructure/Dashboards/DashboardConfigurationService.cs` | Server-side validation of saved content references and global/public rules. |
| `src/HealthMonitoringApp.Infrastructure/DependencyInjection.cs` | Register new scoped service. |
| `src/HealthMonitoringApp.WebUI/Components/Pages/Home.razor` | Add/preview/save/cancel reference-backed dashboard cards and report rendering. |
| `src/HealthMonitoringApp.WebUI/Components/Pages/Home.razor.css` | Borderless global/personal zone styles and themed item tiles. |
| `src/HealthMonitoringApp.WebUI/Components/Pages/DashboardContentPicker.razor` | Picker, categories, text search, selection badges and removable preview. |
| `src/HealthMonitoringApp.WebUI/Components/Pages/DashboardContentPicker.razor.css` | Large responsive modal styling. |

## Authorization and privacy

- The dashboard requires `pages.dashboard`; catalog checks the actor's live section-level permissions before returning **any** items from that section (`wiki.notes.view`, `wiki.announcements.view`, `wiki.todos.view`, `wiki.queries.view`, `wiki.curls.view`, `reports.view`).
- Personal layouts may reference any content **visible to that actor** under the existing knowledge `Public/Private` model. Private content is accessible to its author and the Admin/Owner roles, subject to section access.
- Global modifications are permitted only for Admin/Owner, checked **again in `SaveGlobalAsync`**, not just in the UI. Only **public** wiki items and permitted reports may be saved globally; private items are rejected at the service boundary. The global picker action is hidden from other roles.
- Rendering saved references re-resolves current permissions and source existence. Deleted, newly private or newly forbidden content won't render to unauthorized viewers; source content is never stored in dashboard JSON.
- Query/code bodies are searched in the database; only a bounded snippet is returned as a card preview, avoiding unnecessarily returning full code values for all search results.

## Test checklist

1. Open `/dashboard`; verify upper and lower zones are flush and empty-state placeholders have no frames, gutters or shadows.
2. Create/select a personal dashboard, enter Edit, click Add content; verify wide modal, category rail, search bar, result list and responsive behavior.
3. Search a term that appears **only inside** a note block, an individual todo line, an SQL body or a cURL body. Check announcement text and report job/field searches.
4. Select several different types and check 1, 2, 3… corner badges. Toggle an item off. Open Preview, remove an item, verify count updates.
5. Add to personal: new cards appear immediately. Cancel dashboard edit: staged cards disappear. Repeat then Save and refresh: they persist.
6. Use another personal dashboard from the dropdown: its layout changes while global stays unchanged. Set it as default; refresh.
7. As User, confirm the global action is absent. As Admin and Owner, confirm present. Attempt to add a private document globally: the UI should reject. Attempt a forged Global save: backend rejects.
8. Sign in as an account without access to a particular wiki section: that section's items never appear in search or resolution, even if global references exist.
9. Delete or make a saved document private, sign in as another user, refresh; verify its card is absent. Check a report card with actual chart data.
10. Try duplicate references, too many references, and invalid `type`/`id` JSON in a direct service call; confirm rejected.

## Build/deployment note

This is a source-only update: no database migration necessary. This environment does not provide the .NET SDK or SQL Server, so a real `dotnet build`, EF Core database query smoke test, and browser verification **could not be run here**. Build and execute the checklist locally before production deployment.
