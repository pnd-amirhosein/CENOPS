# Glasswork 2.13 — Todo, Announcement and Document Visibility

Base: Glasswork 2.12.6.

## Scope

- Existing report wizard measurement chips no longer break onto multiple lines. Overflow is scrollable horizontally.
- **Todo** is a new independent entity and service (`Todo`, `TodoItem`, `ITodoService`, `TodoService`). A todo has a title, author, visibility, soft deletion, and ordered checkbox/text items. The editor reuses Note's plus/drag/delete controls and existing `cenopsNoteDrag` JavaScript without cloning the drag implementation. Text direction is detected from the first strong script character on each row.
- **Announcement** is an independent entity and service (`Announcement`, `IAnnouncementService`, `AnnouncementService`). Creation and editing happen **inline on library cards**, without another editor route. Author selection follows the existing privileged author-selection policy.
- Sidebar entries under `دانش‌نامه توسعه`: `/wiki/todos` and `/wiki/announcements`. Todo editor routes `/wiki/todos/new`, `/wiki/todos/edit/{id}`. Skyline Ctrl+B → **T** opens Todo creation; Ctrl+B → **A** opens the inline new Announcement card.
- Todo uses green and Announcement uses warm light brown.
- Shared public/private toggle added to Curl, SQL Query, Note, Todo, and Announcement editors. Default **public**, including all existing items.
- **Private visibility is enforced in services** for list and direct find; only author, Admin or Owner can read private content (subject to the section's access key). Editing/deleting remains author/Admin/Owner. This is not merely UI-side filtering.
- Two new access keys: `wiki.todos.view`, `wiki.announcements.view`. Migration creates these and grants them to active built-in User/Admin/Owner roles. Newly created built-in roles receive them through the default role seeder.

## Database

Migration: `20261013163000_AddTodosAnnouncementsAndVisibility`.

- Creates `Todos`, `TodoItems`, `Announcements` with FK/indexes and soft-delete support.
- Adds `Visibility tinyint not null default 0` to `Curls`, `Queries` and `Notes`.
- Adds new access records and role mappings idempotently.
- Existing items are not made private and are not deleted.

Apply after backing up the database:

```bash
dotnet ef database update --project src/HealthMonitoringApp.Infrastructure --startup-project src/HealthMonitoringApp.WebUI
```

### New service abstractions

- `ITodoService.ListAsync/FindAsync/AuthorsAsync/CanChooseAuthorAsync/CanManageAsync/SaveAsync/DeleteAsync`
- `IAnnouncementService.ListAsync/FindAsync/AuthorsAsync/CanChooseAuthorAsync/SaveAsync/DeleteAsync`

Both services are registered in `DependencyInjection.AddInfrastructure`.

## Test checklist

1. Run migration, build, verify all five editors show the visibility toggle. Existing Curl/SQL/Note records remain public.
2. As User A, create private Todo/Announcement/Note/Curl/SQL. As User B, ensure list excludes all five and direct ID lookup returns nothing. Admin/Owner should see them. Check edit/delete denial for User B.
3. Create a Todo with Persian and English entries. Confirm checkbox aligns right/left accordingly, and dragging/reordering/deleting persists after reload.
4. Verify Todo list card preview, edit and delete confirmation. Confirm green styling.
5. Create/edit/delete Announcement **inline**, select author as Admin/Owner, verify non-manager cannot impersonate an author, confirm light-brown styling.
6. Ctrl+B then T/A, including permission-based menu hiding. Check inline announcement creation from a deep link.
7. Verify report measurement badges do not split and remain accessible by horizontal scrolling on small widths.

## Verification limit

The environment did not provide a .NET SDK, SQL Server, or a running Blazor browser session. Source integration checks and ZIP CRC checks are provided; a local `dotnet build` and live UI/security checks remain necessary.
