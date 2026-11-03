# NTSW CENOPS — Access Control Developer Guide

> Applies to Glasswork 1.7+. Keep this document in the project for future modules, routes, UI controls, and permission data insertion.

## 1. How it works

```text
User ── many-to-many ── Role ── many-to-many ── Access
          AspNetUserRoles          RoleAccesses
```

- `AspNetUsers`: users (including soft-deleted users).
- `AspNetRoles`: roles (`Owner`, `Admin`, `User`, and custom roles).
- `Accesses`: permissions identified by a unique code/key.
- `RoleAccesses`: role-to-permission assignments.
- A user receives the **union** of permissions of all their active roles. The check is **OR** across roles, not AND.
- Permission checks use current database role assignments and active access records, not a static permission list in the authentication cookie.
- A permission that exists but is assigned to no active role grants nobody access.

## 2. Initial setup

From the solution root, apply the migration already included in Glasswork 1.7:

```powershell
dotnet ef database update --project src/HealthMonitoringApp.Infrastructure --startup-project src/HealthMonitoringApp.WebUI
```

Do **not** run `migrations add` for this existing migration. It creates `Accesses` and `RoleAccesses` and seeds the initial permissions. The project also includes `scripts/SeedBasicAccesses.sql` for reapplying starter grants when necessary; it is not normally needed after migration.

## 3. Add a new permission

Use a stable, descriptive key such as `reports.view`, `reports.export`, `wiki.edit`, or `chat.manage`. Prefer `module.action` naming; do not use the Persian display title as the key.

1. Sign in as **Owner**.
2. Open **Settings** (cog) → **دسترسی‌ها**.
3. Choose **Add**, enter the permission code and other required fields shown in the table, and save.
4. Go to **نقش‌ها**, open the target role's `⋮` menu, and choose **مدیریت دسترسی‌های نقش**.
5. Check the new permission and save. Repeat for other roles as needed.
6. Verify the intended user has at least one of those roles.

A new permission needs no hardcoded policy registration: the custom authorization policy provider resolves permission keys dynamically.

**Example:** To allow report viewing to `Admin` and `Owner`, create `reports.view` and assign it to those two roles. Do not assign it to `User`.

## 4. Protect a page / route

In the routed `.razor` component:

```razor
@page "/reports"
@using HealthMonitoringApp.Domain.Authorization
@attribute [AccessAuthorize("reports.view")]

<h1>گزارش‌ها</h1>
```

This is essential even if the sidebar item is hidden. It protects direct navigation to `/reports`.

## 5. Show or hide an element

Use the reusable `AccessGate` component:

```razor
<AccessGate AccessKey="reports.export">
    <button @onclick="ExportAsync">خروجی گزارش</button>
</AccessGate>
```

For navigation:

```razor
<AccessGate AccessKey="reports.view">
    <NavLink href="/reports">گزارش‌ها</NavLink>
</AccessGate>
```

A gate controls **visibility**, not server-side authorization. Never rely on hiding a button as the only protection for a sensitive operation.

## 6. Check access from C# / services

Inject `IAccessService` where the current `ClaimsPrincipal` is available:

```csharp
// Illustrative use inside a component/service with an available principal:
var canViewReports = await accessService.HasAccessAsync(principal, "reports.view");
var keys = await accessService.GetCurrentAccessKeysAsync(principal);
```

Use the existing project's DI registration and authorization infrastructure. For a sensitive service action (export, delete, manage permissions), **authorize again on the server at execution time**. Route authorization only guards navigation; an already-open page does not automatically prevent later calls after permission revocation.

## 7. Connect a user to an access

The relationship is **indirect**: User → Role → Access. There is no direct UserAccesses relationship in this design.

1. In Settings → **کاربران**, open the user's `⋮` menu → **ویرایش نقش‌های کاربر**.
2. Assign the role that carries the required access and save.
3. If the role lacks that permission, an Owner can assign it through the role's **مدیریت دسترسی‌های نقش** checklist.
4. Refresh/navigate and test the route, gated controls, and protected operation as the target user.

Example: user `ali` has role `ReportReader`; role `ReportReader` has access `reports.view`; therefore `ali` can view `/reports`. If `ali` has multiple roles, permissions from all active roles are combined.

## 8. Built-in permissions

| Access key | User | Admin | Owner |
|---|:---:|:---:|:---:|
| `pages.dashboard` | Yes | Yes | Yes |
| `pages.contacts` | Yes | Yes | Yes |
| `pages.health-apis` | Yes | Yes | Yes |
| `pages.discrepancy-goods` | Yes | Yes | Yes |
| `pages.weather` | Yes | Yes | Yes |
| `settings.view` | No | Yes | Yes |
| `settings.access.manage` | No | No | Yes |

The cog and `/settings` use `settings.view`. The Access tab and Access CRUD require `settings.access.manage` **and** the Owner role; granting the key to Admin alone does not unlock them. Owner's critical settings permissions cannot be revoked.

## 9. Seeding new access data

**Preferred for day-to-day changes:** create the permission in the Access DataTable and assign it to roles through the UI.

**Preferred for repeatable deployments:** add an idempotent, version-controlled seed or migration following the patterns in `scripts/SeedBasicAccesses.sql`. Before writing raw SQL, inspect the current `Accesses`/`RoleAccesses` schema and required columns in the migration/entity definitions. Do not guess IDs, normalized fields, or soft-delete flags. Use stable access keys, look up role IDs by name, check whether each record/relationship exists before inserting, and run in a transaction.

For example, a deployment seed can conceptually do:

```text
Ensure Access(code = "reports.view") exists and is active
Find active roles named "Owner" and "Admin"
Ensure RoleAccesses links each of those roles to reports.view
```

**Do not** run the starter SQL as a general reset of permissions: reapplying grants may restore access that an Owner intentionally removed.

## 10. Editing and deleting access records

- Accesses and roles are soft-deleted, not physically deleted.
- A mapped access cannot be deleted until it is detached from its roles.
- Built-in permission keys are protected against rename/delete.
- Use the management UI for routine changes so validation and notifications are respected.
- Changes to role access are checked on subsequent authorization requests. An already-rendered UI may require navigation or refresh to update its visibility.

## 11. Checklist for every new module

- [ ] Define stable permission keys (e.g. `wiki.view`, `wiki.edit`).
- [ ] Insert the permissions via Access management or a reviewed seed/migration.
- [ ] Assign permissions to the correct roles.
- [ ] Assign those roles to the correct users.
- [ ] Protect every routed page with `[AccessAuthorize("...")]`.
- [ ] Wrap optional menu items/buttons with `<AccessGate AccessKey="...">`.
- [ ] Recheck authorization inside sensitive server-side operations.
- [ ] Test allowed and denied users, including direct URL access and permission revocation.

## 12. Troubleshooting

**The menu item is missing:** Check that the user has an active role, that the role has the required access, and that the UI uses the correct key.

**Direct URL works but the button is hidden:** Check that the page attribute and `AccessGate` reference the same intended permission (they may intentionally differ).

**Permission was granted but not visible yet:** Navigate again or refresh; already-rendered components may not immediately reevaluate their gate.

**Access tab is still hidden for Admin:** This is intentional; the Access management area additionally requires Owner.

**All protected pages fail after deployment:** Ensure the Access Control migration ran before deploying the new binaries.

---

**Rule of thumb:** Create Access → assign to Role → assign Role to User → protect Route + UI + server operation. All three protection layers matter.
