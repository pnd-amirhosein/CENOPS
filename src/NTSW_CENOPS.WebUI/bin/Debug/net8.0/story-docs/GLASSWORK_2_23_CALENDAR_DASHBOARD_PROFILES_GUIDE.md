# Glasswork 2.23 — Calendar, Dashboard Access, and User Profiles

**Baseline:** NTSW_CENOPS Glasswork 2.22.1  
**Scope:** calendar occurrence rendering, scoped scrolling, expanded report UX, shared-dashboard administration/privacy, skyline quick-create layout, and user profile hover cards.

## Change summary

### 1. Calendar event visibility

- `EventRecurrence.OccursOn(...)` now accepts nonrepeating (`None`) events on their original start date. Previously it returned `false` for those events, so the month/week/day grids were empty even when events were saved.
- Existing occurrence expansion continues to account for date ranges and recurring events. Year view is a clean 12-month navigator **without event markers**.
- The event editor navigates back to the new/updated event's month using the `date=yyyy-MM-dd` query parameter. The stored and queried event dates remain Gregorian; the calendar UI renders Jalali dates.
- The calendar application surface is flex-bounded to the viewport; scrolling belongs to the yearly/monthly/timeline view containers, not to a second outer page scroller.

### 2. Report chart presentation

- The expanded `MonitoringReportChart` uses `AppModal.FloatingClose` to display a standalone **top-left X** instead of a redundant footer `بستن` button. This is an optional, reusable `AppModal` feature.
- Nonradial SVG tick labels are counter-scaled using their measured rendered plot height; expanded charts refresh their measurements after opening. Radial charts have a reasonable maximum visual size to avoid extremely large text.

### 3. Shared dashboard deletion

- A trash button appears beside the selected **user-created** dashboard only for its creator, Admin, or Owner. It never deletes the system-wide global dashboard.
- The UI requires explicit confirmation. `IDashboardConfigurationService.DeletePersonalAsync(...)` repeats the authorization on the server.
- Associated **viewer default preferences** referencing the dashboard are cleared before deletion, avoiding FK restriction failures. Underlying wiki/report content is not deleted.

### 4. Redacted private dashboard cards

- `IDashboardContentService.ResolveAsync(...)` retains one tile per saved dashboard reference, even if it is inaccessible to the current viewer.
- Inaccessible cards are returned as **redacted placeholders** with a generic title and `IsRestricted=true`, without exposing the original title, summary, author, tasks, code or report details.
- The renderer replaces the card body with a lock icon and Persian no-access message. Its position/dimensions remain unchanged.
- The save validator prevents persisted layouts from acquiring inaccessible references. This does not weaken the existing author/Admin/Owner wiki access checks.

### 5. Skyline quick-create menu

- The quick-create menu uses a CSS grid with six rows; the seventh authorized action flows into a second RTL column. Shortcuts and per-feature authorization are preserved. On narrower viewports the menu can scroll horizontally.

### 6. User hover profiles and DM

- Added `IUserHoverProfileService` and an EF-backed, read-only implementation that returns an authenticated user's **display name, username, and optional Gregorian birthdate**; the hover component formats birthdate in Jalali.
- Added reusable `<UserHoverName DisplayName="..." Username="..." />`, optionally accepting `ChildContent` where the original visible name is itself a clickable button.
- Applied the component to dashboard authors/ownership, wiki lists, chat members/message authors, calendar event details, report/content preview headers, and user administration/profile menus.
- The `پیام مستقیم` button appears only when the viewer has chat access and the target is not the current user. It requests the existing `ChatWorkspaceState.OpenDirect(username)` flow; `ChatWorkspace` consumes the request and uses `GetOrCreateDirectAsync` to select the user's DM, whether the chat is already open or newly mounted.
- The profile service never includes password hashes, security tokens, or unrelated internal user fields.

## Primary changed files

- `src/HealthMonitoringApp.Infrastructure/Events/EventRecurrence.cs`
- `src/HealthMonitoringApp.WebUI/Components/Pages/TeamCalendarPage.razor(.css)`
- `src/HealthMonitoringApp.WebUI/Components/Pages/TeamEventEditorPage.razor`
- `src/HealthMonitoringApp.WebUI/wwwroot/css/operations-shell.css`
- `src/HealthMonitoringApp.WebUI/Components/Pages/MonitoringReportChart.razor`
- `src/HealthMonitoringApp.WebUI/Components/Common/AppModal.razor(.css)`
- `src/HealthMonitoringApp.WebUI/wwwroot/css/monitoring-reports.css`
- `src/HealthMonitoringApp.Application/Dashboards/DashboardModels.cs`
- `src/HealthMonitoringApp.Application/Dashboards/DashboardContentModels.cs`
- `src/HealthMonitoringApp.Infrastructure/Dashboards/DashboardConfigurationService.cs`
- `src/HealthMonitoringApp.Infrastructure/Dashboards/DashboardContentService.cs`
- `src/HealthMonitoringApp.WebUI/Components/Pages/Home.razor(.css)`
- `src/HealthMonitoringApp.WebUI/Components/Pages/DashboardTile.razor(.css)`
- `src/HealthMonitoringApp.WebUI/Components/Layout/Skyline.razor.css`
- `src/HealthMonitoringApp.Application/Profiles/UserHoverProfile.cs` (new)
- `src/HealthMonitoringApp.Infrastructure/Identity/UserHoverProfileService.cs` (new)
- `src/HealthMonitoringApp.Infrastructure/DependencyInjection.cs`
- `src/HealthMonitoringApp.WebUI/Components/Common/UserHoverName.razor(.css)` (new)
- `src/HealthMonitoringApp.WebUI/Components/Chat/ChatWorkspaceState.cs`
- `src/HealthMonitoringApp.WebUI/Components/Chat/ChatWorkspace.razor`
- `src/HealthMonitoringApp.WebUI/Components/_Imports.razor`
- Relevant wiki, admin, profile, and preview components.

## Data model and migration

**No additional EF migration for Glasswork 2.23.** BirthDate and CalendarEvents were introduced in Glasswork 2.21/2.22; those migrations must already have been applied before testing this update. This change adds only a read-only profile projection/interface and a dashboard service operation.

## Local verification checklist (required)

1. Run `dotnet build` against the full solution. Resolve any Razor errors revealed by the compiler before deploying.
2. From a migrated database, create a one-time event and a recurring event on non-today dates; verify each in **month**, **week**, and **day** views; ensure year view has no event markers. Edit an event, save, and verify the editor returns to the correct event month.
3. Switch through all four calendar modes. Check that the **outer application page doesn't scroll** while the active calendar viewport may scroll internally, including responsive layouts.
4. Expand different chart types. Confirm footer Close is gone, top-left X/Escape work, and labels remain reasonably sized in both expanded and resized dashboard cards.
5. As creator, Admin, Owner, and an unrelated User, inspect and attempt dashboard deletion. A nonprivileged user must fail at the **service** even if bypassing UI. Ensure deleting a default dashboard resets affected preferences without deleting any wiki content.
6. Create a private note/todo/SQL/cURL/announcement in a shared dashboard. As a different nonmanager, confirm the same geometry is shown but **no identifying content or author** is delivered or rendered. Verify public cards remain visible.
7. Open quick-create menu with all seven or more authorized actions; verify the seventh appears in the second column and keyboard shortcuts still work.
8. Hover authors in wiki/dashboard/event/chat. Verify profile fields, Jalali birthdate, absent birthdate label, and DM launch to the **correct** user. Test opening a DM with the chat closed and with it already open.

## What has been checked here

Archive integrity and targeted static/source checks are performed before packaging. **No .NET SDK or SQL Server is available in this environment**, so a real C#/Razor compilation, EF/database execution, and full Blazor/browser integration must still be performed locally; none of those are claimed as passed.
