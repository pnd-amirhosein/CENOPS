# Glasswork 2.18 — Interactive Shared Dashboards

**Base:** `NTSW_CENOPS_Glasswork_2_17.zip` (provided source).  
**Scope:** Picker announcement cleanup, reusable preview, resize/reposition interactions, saved responsive widget layout, dashboard actions, fixed global naming, and openly browseable user-created dashboards.

## 1. Changes

### Content picker

- **Announcements** are *a single body of text*, not `title + excerpt`. `DashboardContentService` provides `Title = Content`, `Summary = ""`. The compact announcement card shows the message **once** in a bold, single-line ellipsis heading. The full preview loads the entire message from the existing announcement service and shows it **once** as its prominent heading; its kicker shows category and author.
- Fixed author footer alignment by removing the top border and excess right padding. The author is bolder **without enlarging the font**; eye icon retains its own bottom-left target.
- Extracted a **shared, permission-aware `DashboardContentPreview` component** so the picker eye button and dashboard `مشاهده محتوا` action use identical read-only detail views (including notes, complete todo lists, SQL/cURL values, and reports). The preview has a single X close control; no redundant close-footer action.

### Dashboard layout and card interactions

- Each zone uses its own **ordered CSS grid**. Grid slots are fixed by column and row units; widgets cannot float freely. In edit mode, hover or focus reveals a drag handle. Dropping onto another tile moves the dragged card relative to that tile and stages the new sequence.
- Resizing is staged with pointer handles on the **physical left edge (width)**, **bottom edge (height)**, and **bottom-left corner (both)**. Width is 1–6 columns; height is 2–12 row units. Columns responsively collapse to 4 on tablets and 2 on narrow displays; existing widths are clamped visually, not overwritten in the database simply by viewing a smaller screen.
- Dragging and resizing go from JavaScript to named `[JSInvokable]` methods; **only Blazor** owns and saves `LayoutJson`. Native HTML drag/drop is initialized on explicit handles; pointer capture supports resizing. `Alt+Arrow` on a focused drag handle reorders; arrow keys on focused resize handles resize.
- Delete action uses a proper trash SVG, visible only on tile hover/focus in edit mode (or always available on touch). A confirmation dialog explains it removes a dashboard reference, **not** the underlying wiki item. Save persists; Cancel rolls back.
- Notes, todos, SQL and cURL use the same *information layouts* as the picker: notes show short descriptions, todos their first three one-line items, SQL/cURL show description and monospaced code prefixes. Sizes can be changed without duplicating content data.
- **Announcements render as borderless typographic banners** with category and author in pink above large centered, dark announcement text. No outer card background/border, no view-content button or click-to-preview on dashboard.
- Report widgets keep the existing `MonitoringReportChart`, with a scrollable/responsive container and a separately accessible read-only preview. The chart's existing expand behavior remains.
- Removed `CONTROL CENTER / DASHBOARDS` and the redundant `داشبورد` heading from the page top bar. Buttons remain.
- The global area is **always** labeled `داشبورد عمومی`; no edit input, and the configuration service always saves this fixed label.

### Public selection versus edit permissions

User-created dashboards are now **browseable by every authenticated user who has Dashboard access**. The dropdown shows dashboard name and creator. The selected dashboard's creator can rename and edit its personal/content area; everyone else can browse only. Admin/Owner can still edit the **global** area regardless of which dashboard is currently selected.

The **gold star is per viewer**: choosing someone else's dashboard as a default must not change the creator's default. New entity `DashboardViewerPreference` stores `(UserId, DashboardId)` with a unique primary key on `UserId`. Existing owned defaults are honored when a user has not yet set an explicit preference. Existing dashboard `IsDefault` values are retained for fallback compatibility but are **not modified** on new default selections.

**Privacy:** Browsable dashboard configurations do *not* grant access to the underlying content. Dashboard cards still resolve through `IDashboardContentService`, which independently checks dashboard access, section access, author/visibility, and soft deletion. Thus a dashboard containing private wiki content may show fewer tiles for other viewers. Global content remains public-only and editable solely by Admin/Owner; server enforcement remains active.

## 2. Persisted layout format

Backward-compatible JSON array in existing `LayoutJson` columns:

```json
[
  {"type":"note", "id":9, "w":2, "h":4},
  {"type":"announcement", "id":12, "w":3, "h":2},
  {"type":"report", "id":5, "w":3, "h":6}
]
```

- **Array order is the tile order/slot position**. Drag/drop reorders that array; no absolute coordinates are used.
- `type` and `id` are stable references; `w`/`h` are validated grid spans.
- **Older** `[{"type":"note", "id":9}]` values are read and normalized to type-specific defaults. No migration is required for existing `LayoutJson` values. `VerifiedLayoutAsync` only accepts `type`, `id`, `w`, `h`, valid sizes, no duplicates, and a maximum of 60 items per zone; existing source access checks still apply.
- Default size: note 2×4, announcement 3×2, todo 2×5, SQL/cURL 3×5, report 3×6.

## 3. Required database migration

**This version has a database change** for per-user default selection. The included migration is:

`src/HealthMonitoringApp.Infrastructure/Migrations/20261015110000_SharedDashboardViewerPreferences.cs`

EF Core model and snapshot were updated in the source. Back up the database and apply from the solution root with the usual SQL Server connection/configuration:

```bash
dotnet restore NTSW_CENOPS.sln
dotnet build NTSW_CENOPS.sln
dotnet ef database update --project src/HealthMonitoringApp.Infrastructure --startup-project src/HealthMonitoringApp.WebUI
```

Your `dotnet-ef` tooling must be available locally. The new table references `AspNetUsers` and `UserDashboardConfigurations`. Its `DashboardId` FK is `Restrict` (to avoid SQL Server cascade-path conflicts). If you later implement deletion of dashboards, delete/update referencing viewer preferences **before** deleting a dashboard.

## 4. Key files

| File | Purpose |
| --- | --- |
| `Application/Dashboards/DashboardContentModels.cs` | Width/height-compatible dashboard references |
| `Application/Dashboards/DashboardModels.cs` | Creator/ownership/read-only access flags for browseable dashboards |
| `Infrastructure/Dashboards/DashboardContentService.cs` | Announcement content only once |
| `Infrastructure/Dashboards/DashboardConfigurationService.cs` | Shared listing, per-viewer default, width/height validation, fixed public title, creator-only writes |
| `Infrastructure/Dashboards/DashboardEntities.cs` | Viewer preference table entity |
| `Infrastructure/Persistence/AppDbContext.cs` | Entity configuration and keys |
| `Infrastructure/Migrations/*ViewerPreferences.cs` and `AppDbContextModelSnapshot.cs` | EF database migration |
| `WebUI/Components/Pages/DashboardContentPicker.razor(.css)` | Updated announcement and author UX, common preview integration |
| `WebUI/Components/Pages/DashboardContentPreview.razor(.css)` | **New** unified permission-aware detail preview |
| `WebUI/Components/Pages/DashboardTile.razor(.css)` | **New** reusable flexible tile display, controls and type-specific layouts |
| `WebUI/Components/Pages/Home.razor(.css)` | Dashboard editing, layout staging, confirms, permissions, compact toolbar |
| `WebUI/wwwroot/js/dashboard-grid.js` | **New** native drag/drop, pointer resize, keyboard handling |
| `WebUI/Components/App.razor` | New JavaScript initialization |

## 5. Local integration test checklist

1. Compile/migrate first. Sign in as each of Owner, Admin, ordinary User. Verify public dashboard can be edited only by Owner/Admin and user-created dashboard only by its creator. A user viewing another user's dashboard must **not** be able to edit it.
2. User A creates dashboard A; User B sees it in the dropdown. B selects A and chooses gold star. Reload both users: B's default changes; A's default remains untouched. Verify *both* can view public tiles.
3. Add multiple card types in edit mode, reorder via drag handle (and Alt+Arrows), resize from left/bottom/corner and verify all three actions. Save/reload to verify exact order/dimensions. Cancel and verify layout and deleted tile staging roll back.
4. Test desktop (6 columns), tablet (4), mobile (2); no viewport horizontal overflow, no accidental new columns from oversized saved tiles. Resize on mobile only when intended.
5. Hover a card: trash icon appears; clicking opens confirmation. Cancel leaves tile. Confirm removes tile from draft but does not delete source. Save and refresh; it stays removed.
6. Announcement in picker shows **one** bold line; eye preview shows complete message **once**. Dashboard announcement is borderless, centered and not clickable for details; author/category use pink.
7. Picker author has no separator line and no accidental right inset. Preview other cards from both picker and dashboard; verify full note/todo/code/report data is loaded, and source authorization is respected.
8. Report tile after resize: chart remains readable and its existing expand mode remains usable; the new preview action opens the common report detail.
9. Global title is always `داشبورد عمومی`. The redundant top-of-page heading is absent; dashboard selectors and actions remain.
10. Create private note content as User A and add to their dashboard. User B must **not** see that private item's contents through dashboard or preview. Existing source permissions must remain authoritative.

## 6. Verification completed here

- Started from the provided 2.17 archive. JavaScript passed `node --check`; modified CSS parsed without stylesheet errors using `tinycss2`.
- A Chromium **static fixture** rendered the new grid at 1350 / 875 / 410 px, without horizontal overflow. It checked responsive column spans and exercised JavaScript callback semantics for drag reordering, keyboard movement/resizing, and pointer corner resizing. This is *not* a running Blazor/EF integration test.
- **Not verified here:** real .NET build, EF migration against SQL Server, runtime Razor behavior, or live content-permission tests: .NET SDK and SQL Server are not available in this container.

**Next step:** run migration + local build, then test both shared dashboard permissions and the drag/resize/save interactions on your deployed Blazor app.
