# Glasswork 2.16 — Dashboard Card Picker & Wiki Fixes

**Baseline:** NTSW_CENOPS Glasswork **2.15** (the most recently supplied source).

**Scope:** dashboard *Add content* picker refinements, per-card read-only previews, bounded todo previews, and soft-delete consistency across wiki libraries. The dashboard storage model, roles/access infrastructure, and existing dashboard selection/save flow remain unchanged.

## 1. Add-content modal

- Increased desktop modal width from 1,180 px to **1,440 px** (still limited by viewport), with a tighter responsive layout on smaller displays.
- Widened the category rail to **235 px** on desktop. Category labels remain on **one line** with ellipsis when space is insufficient; they no longer wrap. Replaced symbolic category glyphs with consistent stroke-based SVG icons for All, Notes, Announcements, Todos, SQL, cURL and Reports.
- Replaced the close-character glyph with a centered SVG cross; corrected the centering and square dimensions of selection-number badges and plus icons.
- Changed cards from a single large `<button>` to an `<article>` with **separate Select and Preview buttons**. The eye icon now opens details without toggling selection.
- Preserved each content type's color, while differentiating cards further with subtly different edge treatments, gradient backgrounds and a dashed treatment for todos.
- Clicking elsewhere inside the modal dismisses the *selected-items preview* popover. Clicking inside the popover (e.g. Remove) does not dismiss it.
- **Unsaved-selection protection:** when selection count is nonzero, attempting to close via **X**, **Cancel**, **backdrop click**, or **Escape** first asks for confirmation. *Continue selecting* preserves all selections; *Close and discard* closes the picker. With zero selections, the picker closes immediately.
- Escape is handled centrally: if a card detail preview is showing, close it first; otherwise if the selection popover is open, close the popover; otherwise request to close the picker. When discard confirmation is open, Escape dismisses the confirmation and preserves the selections.

### Read-only card details

The eye button opens a scrollable, permission-aware detail dialog over the picker. Previews always load **actual source content**, not only the search summary.

| Type | Preview |
| --- | --- |
| Note | Full note rendered with the existing `NoteContent` component, including text/code blocks |
| Announcement | Full announcement text |
| Todo | Each item and its completed state |
| SQL | Description and full SQL body as escaped, scrollable text |
| cURL | Description and full cURL request as escaped, scrollable text |
| Report | Job, chart type, comparison field, measure fields and (when available) a read-only chart |

Preview data comes from the existing authorized services, under the signed-in actor. Report chart expansion is disabled **in the nested preview only**; normal report charts retain their existing expand action. Stale concurrent detail responses are ignored when the dialog is closed or another card is opened. Missing/deleted/inaccessible source items show an error rather than leaking content or activating selection.

## 2. Wiki fixes

### Todo preview overflow

`TodoLibrary` now passes `CssClass="todo-preview-modal"` into `AppModal`. The `todo-preview-modal` surface is capped to the visible viewport and uses a flex column layout with a **vertically scrollable body**. Its heading and footer remain visible while long lists scroll. Mobile viewport sizing is handled as well.

### Deleted cards lingering in lists

Notes, Announcements, Todos, SQL and cURL are **soft-deleted** using `IsDeleted`. The existing EF Core global query filters remain in place, and the individual service **list, find, edit and delete** paths now explicitly check `!IsDeleted` to guard against stale/deleted entries. The dashboard content catalog also explicitly excludes deleted wiki entries from search and resolved dashboard references.

After successful deletion, all five wiki library components immediately remove that ID from their local list, revoke local edit-action membership, clear open contextual state, and then refresh from the server. This prevents a non-interactive deleted card from remaining on screen even if a subsequent fetch fails.

**No database migration needed.** These are application queries and UI behavior changes over existing fields.

## 3. Updated files

**WebUI**

- `Components/Pages/DashboardContentPicker.razor`
- `Components/Pages/DashboardContentPicker.razor.css`
- `Components/Pages/MonitoringReportChart.razor` (optional `AllowExpand`, default `true`)
- `Components/Tasks/TodoLibrary.razor`
- `Components/Notes/NoteLibrary.razor`
- `Components/Announcements/AnnouncementLibrary.razor`
- `Components/Knowledge/KnowledgeLibrary.razor`
- `Components/App.razor` (static asset cache version)
- `wwwroot/css/note-workspace.css`
- `wwwroot/js/site.js`

**Infrastructure**

- `Knowledge/KnowledgeServices.cs`
- `Notes/NoteService.cs`
- `Tasks/TodoService.cs`
- `Announcements/AnnouncementService.cs`
- `Dashboards/DashboardContentService.cs`

## 4. Local smoke-test checklist

1. Run `dotnet restore` and `dotnet build NTSW_CENOPS.sln` on a machine with .NET 8 SDK and the project's usual dependencies.
2. Sign in and open Dashboard → Edit → Add content. Verify desktop width, side rail labels/ellipsis, SVG icons, selection numbers, centered buttons, and six visibly distinct card styles.
3. Select two or more cards. Confirm that the preview menu counter matches, individual Remove buttons work, and clicking elsewhere **within the modal** dismisses the menu.
4. Try closing by X, Cancel, overlay click, and Escape with selected items. Confirm discard prompt. Choose Keep to preserve selection, Discard to close. With no selection, close directly. With a card preview open, Escape first closes the preview.
5. Open the eye preview for each of six types. Check full note blocks, todo statuses, announcement text, full SQL/cURL bodies, report metadata and read-only report chart. Verify selection does not change simply by previewing.
6. On wiki Todos, create a long todo list (e.g. 80–120 items) and open its preview. Confirm that the dialog fits the screen, its inner list scrolls and the footer can be reached.
7. Create then delete a note, announcement, todo, SQL, and cURL document. Verify immediate disappearance, then refresh the route/revisit: the deleted card must not return. Check that the dashboard picker also excludes deleted wiki items.
8. Verify a restricted/private item cannot be previewed by a user lacking access. Confirm that global-dashboard add remains available only to authorized Admin/Owner users and private content cannot be published to the global dashboard.
9. Save an edited personal dashboard and refresh. Confirm that the changes persist without disturbing the global dashboard.

## 5. Verification performed in this environment

- Source diff verified against supplied 2.15 ZIP, preserving the rest of the project.
- JavaScript syntax checked with Node (`node --check`).
- Modified CSS parsed without syntax errors using `tinycss2`.
- Layout tested as a **static Chromium mock** at desktop (1440 px), tablet (630 px), and mobile (380 px) viewport widths. These are styling checks, **not** a real Blazor runtime test.
- ZIP integrity is checked when the release archives are created.

**Not executed here:** `dotnet build`, EF Core migrations/database I/O, live authorization tests and Blazor interaction tests. The SDK/database aren't available in this environment, so please validate those locally before deployment.
