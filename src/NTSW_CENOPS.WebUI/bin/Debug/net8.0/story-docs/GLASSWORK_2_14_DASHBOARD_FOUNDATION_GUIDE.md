# Glasswork 2.14 — Dashboard configuration foundation and wiki polish

Based on Glasswork 2.13.1.

## Dashboard workflow

- `/dashboard` has two visually equal zones: **Global** (top, identical for all accounts) and **Personal** (bottom, loaded by signed-in user).
- In the top-left toolbar, the searchable dashboard selector lists only the user's own configurations. `+` creates an additional personal dashboard.
- **Edit** replaces selector/create/default/edit with **Save** and **Cancel**, without changing the global content selected by the user.
- Editing currently allows naming/renaming a personal dashboard, and renaming the shared global area for Admin/Owner only. No draggable widgets have been invented: the `LayoutJson` is persisted but remains `[]` until the widget contract is specified.
- **Set as default** persists the selected dashboard per account; a default is selected on page load. Other users' defaults are unaffected.
- Global and personal configurations are separate records and are never merged into a single mutable document.

## Database schema

`GlobalDashboardConfigurations`:
- `Id int PK`, stable global row 1 seeded by migration
- `Title nvarchar(120)`
- `LayoutJson nvarchar(max)` (JSON array of future widgets)
- `UpdatedAtUtc datetimeoffset`

`UserDashboardConfigurations`:
- `Id int identity PK`
- `UserId int FK` to AspNetUsers
- `Name nvarchar(120)`
- `LayoutJson nvarchar(max)`
- `IsDefault bit`, at most one TRUE per user (filtered unique index)
- `UpdatedAtUtc datetimeoffset`

EF migration: `20261014100000_AddDashboardConfigurations`.

Run after backing up database:

```sh
dotnet ef database update --project src/HealthMonitoringApp.Infrastructure --startup-project src/HealthMonitoringApp.WebUI
```

## Services

`IDashboardConfigurationService`:
- `GetWorkspaceAsync(actor)` — returns global and only actor's own personal dashboards, their default, and whether they may edit global.
- `CreatePersonalAsync(name, actor)` — add personal dashboard, first becomes default.
- `SavePersonalAsync(id, name, layoutJson, actor)` — owner-only update.
- `SaveGlobalAsync(title, layoutJson, actor)` — current Admin/Owner roles verified against DB.
- `SetDefaultAsync(id, actor)` — checks ownership; transaction ensures one default per user.

Every public service operation requires live `pages.dashboard` access. Layout input is validated as a JSON array and capped at 128,000 characters. The user ID comes from the authenticated principal, not the caller input.

## Wiki control and theme fixes

- All ordinary checkboxes are constrained to square dimensions; Todo uses a compact 16x16 size, with green checkboxes.
- Todo editor overrides the Note editor's explicit orange backgrounds, insertion lines, buttons, drag handles, hover/focus, and drop indicators.
- Color audit: cURL cyan; SQL purple; Notes orange; Todo green; Announcements raspberry/rose.
- List/new editor stylesheet cache keys updated to 2.14.

## Local testing checklist

1. Run EF migration on a copy of your SQL Server database and launch the app.
2. Create two dashboards as User A, switch between them; confirm the GLOBAL title/content doesn't change.
3. Set the second as default and refresh. It should be selected.
4. Sign in as User B; User A's dashboards must not appear and User B must not be able to update them via the service.
5. Rename a personal dashboard and Save; verify persisted. Change another name and Cancel; verify no change.
6. Sign in as User then as Admin or Owner; User cannot change global settings, Admin/Owner can.
7. Check checkbox 1:1 sizing in DataTable, Todo, wiki, settings and light/dark layouts.
8. Check wiki list/new color identities for cURL, SQL, Note, Todo, Announcement.
9. Confirm the Todo drag indicator, add button and checkbox are green; Notes stays orange.

## Verification limitations

Static checks passed, but a .NET SDK and SQL Server are unavailable in this environment. No live Blazor compile or migration execution was performed. The dashboard layout/drag/widget rendering is intentionally not implemented yet.
