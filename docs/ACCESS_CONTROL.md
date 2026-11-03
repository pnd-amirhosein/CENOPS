# NTSW CENOPS — Database-backed access control (Glasswork 1.7)

## Apply migration

From the solution root:

```powershell
dotnet ef database update --project src/HealthMonitoringApp.Infrastructure --startup-project src/HealthMonitoringApp.WebUI
```

**No additional `dotnet ef migrations add` command is needed**: `20261008150000_AddAccessControl` is included.
The migration creates `Accesses` and `RoleAccesses` and seeds seven built-in accesses/grants for all existing `User`, `Admin`, and `Owner` roles. Roles created after migration are given these defaults once at creation by `DefaultRoleAccessSeeder`. Custom role permissions do not get reset on login or app restart.

`scripts/SeedBasicAccesses.sql` is a **separate, idempotent SQL Server script**, useful if you need to reapply the initial grants after roles were created. Normally the migration already handles the startup set.

## Initial permission matrix

| Permission code | User | Admin | Owner |
|---|:---:|:---:|:---:|
| `pages.dashboard` | ✓ | ✓ | ✓ |
| `pages.contacts` | ✓ | ✓ | ✓ |
| `pages.health-apis` | ✓ | ✓ | ✓ |
| `wiki.curls.view` | ✓ | ✓ | ✓ |
| `wiki.queries.view` | ✓ | ✓ | ✓ |
| `pages.weather` | ✓ | ✓ | ✓ |
| `settings.view` (cog + `/settings`) | – | ✓ | ✓ |
| `settings.access.manage` (Access tab/editor/CRUD) | – | – | ✓ |

The Access tab is also explicitly restricted to the **Owner** role and the server validates the role from the **database** before any read/write. Granting `settings.access.manage` to a non-Owner role does not unlock the tab.

The Access tab lives **inside `/settings`**, beside Roles and Users. In the Roles table, choose `⋮ → مدیریت دسترسی‌های نقش` to open the checkbox editor (Owner only).

## How to secure future pages

```razor
@page "/reports"
@using HealthMonitoringApp.Domain.Authorization
@attribute [AccessAuthorize("reports.view")]
```

The custom authorization-policy provider accepts any access key: you do **not** have to edit the policy registration when introducing new accesses. Create the key in the Access DataTable, then assign it to one or more roles in the checkbox editor. Until assigned, access is denied.

## How to hide UI fragments

```razor
<AccessGate AccessKey="reports.view">
    <NavLink href="/reports">گزارش‌ها</NavLink>
</AccessGate>
```

In code, inject `IAccessService` and call `HasAccessAsync(principal, "reports.view")` or `GetCurrentAccessKeysAsync(principal)` for the whole permission set. Both read active user → Identity user roles → active roles → RoleAccesses → active Accesses directly from SQL Server (no cookie permission cache).

**Important:** hiding elements is UX only; authorization on the route is mandatory. Privileged server operations also need a server-side authorization check, as in `AccessService`. The previous `<RoleGate>` component stays available for cases that really need role-only visibility.

## Safety and limitations

- Access records and roles are soft-deleted, never physically removed. Deleting a mapped access is refused; detach it from roles first. Built-in access keys cannot be renamed or removed.
- Owner's `settings.view` and `settings.access.manage` cannot be revoked from Owner to prevent administrator lockout.
- Custom permissions are OR-ed across all active roles of a user.
- Built-in Identity roles (`Owner`, `Admin`, `User`) cannot be renamed to/from another role to prevent privilege escalation through role renaming.
- Route authorization rechecks the database at navigation; already-rendered components may need navigation/refresh before their visibility updates. Long-running server-side operations must check permission when invoked.
- The setting's Users and Roles CRUD remains on the existing service/UI. Its previous UI route restriction is now `settings.view`.
- Do **not** deploy the new binaries before the migration: protected pages will intentionally fail closed if permission tables do not yet exist.
