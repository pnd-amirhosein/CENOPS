# Glasswork 2.32 — Dashboard privacy, editable layouts and note UI

Baseline: **Glasswork 2.31**. This release is a targeted patch; it does not add a database table or migration.

## Changes

### 1. Author versus visibility — server-side rule

Dashboard cards are not hidden merely because their author differs from the viewer. A card can be displayed when its document has **Public** visibility, or when it is private but the viewer is its author or an Admin/Owner. A document that is deleted remains unavailable. Dashboard-resolved public content no longer requires separate permissions to open that module's management page, while catalog browsing/adding new content still requires the module grant.

The permission-aware `FindAsync` paths for notes, todos, announcements, SQL and cURL also permit readers with Dashboard access to open content they may legally see; the existing per-record privacy checks continue to run. Edit, list and delete authorization rules are unchanged. No private payload is stored in layout JSON.

### 2. Existing inaccessible cards can be saved

Existing dashboard references that later become private, inaccessible or soft-deleted may remain in a saved layout. They continue to render as locked/redacted placeholders, preserving position and size. Attempting to add a *new* inaccessible reference is still rejected by the server. The same rule applies to nested smart-card slides; global dashboards still reject newly added non-public references.

On Save, the page re-resolves its references, including nested smart-card references. If any are unavailable it shows a confirmation: continuing preserves those slots and saves the layout; cancelling returns to the editor. Save/Cancel and existing owner/admin permissions are preserved.

### 3. Side-by-side note content in dashboards

Previously note cards displayed only `Note.Excerpt` (derived from the first paragraph), making a saved row appear as a single item. Authorized note cards now load a bounded block preview with `RowGroup` metadata and render it using the reusable `NoteContent` component. This applies to standard dashboard tiles and smart-card slides. The full note remains accessible in the existing detailed preview. Code and table blocks use their existing renderer; the card scrolls inside its assigned size.

### 4. Todo checkbox alignment

Swapped the font-based tick for a centered SVG and normalized the checkbox's layout in both dashboard tiles and smart-card slides.

### 5. Wiki sidebar colors

Added a small semantic `Accent` field to sidebar items and applied each wiki item's existing color identity (cURL cyan, SQL violet, todos green, announcements pink, notes amber). Other navigation items are unchanged.

### 6. Cleaner insert menu icons

Replaced textual glyphs in the reusable note insertion menu with consistently stroked SVG icons for heading, paragraph, code and table. The menu's insertion and dismissal behavior is unchanged.

## Architecture and safeguards

- Dashboard service is still the authorization boundary; no user-provided author ID is trusted.
- Public documents remain visible across authors; private documents are redacted unless owned by the viewer or viewable by Admin/Owner.
- Saving an existing inaccessible card is not authorization to read its contents.
- New inaccessible references, including newly inserted nested references, cannot be inserted by changing JSON directly.
- Persisted layout still consists only of type, ID, geometry and smart-card configuration.
- The note row-preview loads blocks only for notes that passed the database visibility filter.

## Files changed

- `src/HealthMonitoringApp.Application/Dashboards/DashboardContentModels.cs`
- `src/HealthMonitoringApp.Infrastructure/Announcements/AnnouncementService.cs`
- `src/HealthMonitoringApp.Infrastructure/Dashboards/DashboardConfigurationService.cs`
- `src/HealthMonitoringApp.Infrastructure/Dashboards/DashboardContentService.cs`
- `src/HealthMonitoringApp.Infrastructure/Knowledge/KnowledgeServices.cs`
- `src/HealthMonitoringApp.Infrastructure/Notes/NoteService.cs`
- `src/HealthMonitoringApp.Infrastructure/Tasks/TodoService.cs`
- `src/HealthMonitoringApp.WebUI/Components/App.razor`
- `src/HealthMonitoringApp.WebUI/Components/Layout/NavMenu.razor`
- `src/HealthMonitoringApp.WebUI/Components/Notes/NoteInsertMenu.razor`
- `src/HealthMonitoringApp.WebUI/Components/Pages/DashboardContentPicker.razor`
- `src/HealthMonitoringApp.WebUI/Components/Pages/DashboardSmartSlide.razor`
- `src/HealthMonitoringApp.WebUI/Components/Pages/DashboardTile.razor`
- `src/HealthMonitoringApp.WebUI/Components/Pages/Home.razor`
- `src/HealthMonitoringApp.WebUI/Components/SideBar/SidebarItem.cs`
- `src/HealthMonitoringApp.WebUI/Components/SideBar/SidebarLink.razor`
- `src/HealthMonitoringApp.WebUI/wwwroot/css/dashboard-card-components.css`
- `src/HealthMonitoringApp.WebUI/wwwroot/css/note-workspace.css`
- `src/HealthMonitoringApp.WebUI/wwwroot/css/operations-refinements.css`

## Verification performed

- Both ZIP archives checked for integrity.
- CSS parsing completed without errors; csproj XML is well-formed.
- Targeted source assertions checked the save exception policy, smart-card nested handling, note rendering, wiki colors and menu icons.
- A visibility truth-table was checked for public/author/manager and private non-author scenarios.

**Not performed in this environment:** `dotnet build`, EF/SQL-backed integration, or live Blazor interaction testing. These should not be considered passed.

## Local QA

1. Use two normal users, A and B. Add a *public* note from A to A's shared dashboard; log in as B and check that its content and detailed preview are visible. Repeat for all five wiki types.
2. Mark A's note *private*: B should see a locked card in the same position, not the title/snippet; A and managers can see it in personal contexts.
3. Edit the existing dashboard containing the inaccessible card and press Save. Confirm the warning; verify the card geometry survives reload. Repeat for a deleted item and nested smart slide.
4. Attempt to inject a **new** private/deleted reference into layout JSON. The server must reject it.
5. Put two to four note blocks in one inline row; verify they all render in the dashboard and the detailed preview, including a table/code block. Test narrow and wide cards.
6. Test completed todo markers, wiki sidebar accent colors, and each note insertion menu choice.
7. Confirm Cancel does not save any changed dashboard layout.

## Database

**No new migration needed.** Existing 2.29 `RowGroup` migration must already have been applied for inline notes.
