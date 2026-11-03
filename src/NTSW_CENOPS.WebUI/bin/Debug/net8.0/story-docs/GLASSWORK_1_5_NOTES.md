# Glasswork 1.5 — Role badges and identity role management

## Main changes
- Generic `ColumnType.List` supports `IEnumerable` values via the reusable `AppBadge` component. Lists are read-only during normal inline DataTable edits; list filtering and sorting use their contents.
- Users load assigned roles in a bulk Identity `AspNetUserRoles`/`AspNetRoles` relation lookup. Soft-deleted users/roles are excluded by the existing EF query filters.
- Row context menu in Settings > Users: **ویرایش نقش‌های کاربر**. Modal has searchable available/assigned role lists, click to add/remove, cancel without changes, save with refresh.
- Persist role assignments with Identity UserManager transaction, security stamp update, role ID validation and protection against removal of the last Owner. Only Owner can grant/revoke Admin or Owner roles.
- Reusable confirmation AppModal on **all** DataTable create/update/delete actions; cancel edit no longer mutates the loaded item (CloneForEdit configured for each current table).
- Global scoped `AppNoticeService` + `AppNoticeHost`: bottom-left Persian glass toast, closable, success 4 s / error 7 s. Used by Settings, Contacts and Health APIs.
- Soft-delete messages now say «کاربر حذف شد» / «نقش حذف شد» (database deletion remains logical, not physical).
- No schema change; no EF migration needed.

## Security and operational notes
- Settings page remains restricted to Admin and Owner. Admin can adjust non-privileged roles; Owner alone can change Admin/Owner assignments.
- User permission changes update the Identity security stamp; existing sessions will see updates on revalidation or sign-in.
- Both DataTable confirmations are UI-layer safeguards; application services still enforce persistence validation.
- Run locally: `dotnet build NTSW_CENOPS.sln`; test user-role saves, cancel, current-user role changes and the resulting sign-in permissions.
- This environment has no .NET SDK, so live Blazor compilation and browser clicks are not verified.
