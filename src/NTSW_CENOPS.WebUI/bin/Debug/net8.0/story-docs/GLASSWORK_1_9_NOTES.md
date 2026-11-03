# Glasswork 1.9 — Developer Knowledge Library

The project now contains **two independent libraries**, for cURL requests and SQL queries.

## Database

- `Curls`: Id, Title, Name, UserId (author), Description, Value, IsDeleted.
- `Queries`: same columns, independent table and EF entity.
- `Name` is a short alias kept synchronized with `Title` by the editor, avoiding duplicate typing.
- Both have an FK to `AspNetUsers` and soft-delete rather than physical deletion.
- New migration included: `20261008190000_AddDeveloperKnowledge`.
- New access keys: `wiki.curls.view`, `wiki.queries.view`, granted to existing Owner/Admin/User by the migration. Newly created built-in roles get the same grants.
- Old discrepancy access is soft-disabled; historical migration files are retained for EF migration integrity.

Run the existing migration (do not scaffold another):

```powershell
dotnet ef database update --project src/HealthMonitoringApp.Infrastructure --startup-project src/HealthMonitoringApp.WebUI
```

## Routes

| Library | List | New | Edit |
| --- | --- | --- | --- |
| cURL | `/wiki/curls` | `/wiki/curls/new` | `/wiki/curls/edit/{id}` |
| SQL | `/wiki/queries` | `/wiki/queries/new` | `/wiki/queries/edit/{id}` |

Both routes and the shared services check the applicable access policy. The Skyline `+` opens creation shortcuts; the sidebar contains a **دانش‌نامه توسعه** parent section. Search and dashboard shortcuts also point to both new modules.

## Authorization

Any user with access can list, read, and create library entries. **Only Owner, Admin, or the stored author** can update or soft-delete an entry. These checks happen in the database service, not just in Razor. Owners and admins may select a different author. Non-admin contributors can select only themselves. Default author is the current signed-in user; the editor has a shortcut to reset it.

## Implementation

- `Application/Knowledge`: DTO, operation result, and service interfaces.
- `Infrastructure/Knowledge`: separate EF entities and shared service implementation (without connecting the two datasets).
- `Components/Knowledge`: shared card library and editor.
- Pages: thin, permission-guarded route wrappers for each module.
- Shared AppModal confirmation for Save/Cancel/Delete, AppBusyService and AppNoticeService for async and feedback.
- Value is fetched **only on preview/edit**, not during list loading.

**No cURL or SQL code is executed by these pages.** These features store and display documentation snippets only. Treat saved snippets as potentially sensitive; avoid saving passwords, bearer tokens, secrets, and personally identifiable data. Queries are shown as text, not run against the database.

## Local verification checklist

1. Run migration and build solution: `dotnet build NTSW_CENOPS.sln`.
2. Log in with User and verify both list pages and the Skyline `+` work.
3. Create a cURL entry, cancel an edit, then save and verify its author and content.
4. Verify a standard user cannot edit or delete another author's entry (including a crafted request).
5. Verify an Owner/Admin can edit or delete another author's entry and select another author.
6. Repeat CRUD with SQL; confirm SQL and cURL cards are independent.
7. Test empty-state and filtering, confirmations, feedback, and mobile layout.

**Note:** This delivery has static review only; a .NET SDK/live Blazor browser test was unavailable in the packaging environment.
