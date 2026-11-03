# Glasswork 1.8 — Reverse Relationship Management

This release builds on `NTSW_CENOPS_Glasswork_1_7_FULL.zip`.

## Features

1. **Access → Roles:** In Settings → دسترسی‌ها, use `⋮ → مدیریت نقش‌های این دسترسی`. Its searchable checkbox list shows all active roles. Add/remove checkboxes and press ذخیره نقش‌ها. The changes are saved atomically through `IAccessService.UpdateAccessRolesAsync`.
2. **Role → Users:** In Settings → نقش‌ها, use `⋮ → مدیریت کاربران نقش`. Both sides have separate searches; click a user badge to move between available and assigned. Save invokes `IIdentityAdministrationService.UpdateRoleUsersAsync` and updates all affected Identity memberships transactionally; affected users get new security stamps. Admin can edit non-system roles; Owner can additionally edit Admin/Owner.
3. **Reusable DataTable toolbar slot:** Supply `<ToolbarContent>…</ToolbarContent>` on `DataTableToolbar`. Any Razor components or HTML inside render next to the Filter button (on its right in RTL) when the table is in view mode.
4. **Access help:** The new راهنمای دسترسی‌ها toolbar button opens a Persian example guide modal.
5. **Shared checkbox UI:** `Components/Common/Selection/AppCheckboxList.razor` supports checkbox options, selected/disabled IDs, and change notifications. Both permission editors use it.

## Example: Custom toolbar content

```razor
<DataTableToolbar T="MyModel" Controller="_controller" Columns="_columns">
    <ToolbarContent>
        <button type="button" @onclick="ShowHelp">راهنما</button>
        <MyCustomComponent />
    </ToolbarContent>
</DataTableToolbar>
```

## Security

- The Access tab and Access → Roles editor remain Owner-only (live database role and `settings.access.manage` permission checks).
- `settings.view` and `settings.access.manage` can never be removed from the Owner role from either direction; their checkboxes are disabled too.
- Role → Users checks live actor role membership; only Owner can change membership of built-in Admin/Owner roles.
- Removing the last active Owner is blocked.
- Soft-deleted users and roles are omitted.
- Changes do not take effect until Save; errors appear as the existing timed glass notifications.

## Database

**No new migration.** Both link tables (`AspNetUserRoles`, `RoleAccesses`) already existed.

## Local checks

Run `dotnet build NTSW_CENOPS.sln` and test:

1. As Owner, add/remove a role from an access; refresh and verify it persists.
2. Try removing `settings.view` from Owner; verify it is prevented.
3. Add/remove users from a custom role, refresh Users to verify its badges.
4. As Admin, verify editing Owner/Admin membership is unavailable, and test custom roles.
5. Verify both role/access checklists and the help modal on narrow screens.
