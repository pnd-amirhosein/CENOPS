# Glasswork 2.7 — Notes / یادداشت‌ها

Based directly on **NTSW_CENOPS_Glasswork_2_6_FULL.zip**. All existing chat, monitoring, Identity, cURL, SQL and UI code has been retained; this version introduces a block-based Notes library.

## Setup

1. Back up the database.
2. Run `dotnet build` from the solution root.
3. Apply the existing new migration (do not generate another one):

   ```powershell
   dotnet ef database update --project src/HealthMonitoringApp.Infrastructure --startup-project src/HealthMonitoringApp.WebUI
   ```

   Migration: `20261011173000_AddNotes`. This adds `Notes` and `NoteBlocks` and seeds `wiki.notes.view` into `Accesses` and `RoleAccesses` for existing User/Admin/Owner roles. Existing roles created after migration receive the default grant through `DefaultRoleAccessSeeder`.

## Routes and access

- `/wiki/notes`: searchable library, create first card, preview, edit, soft-delete.
- `/wiki/notes/new`: block editor, starts with a required title and author selector.
- `/wiki/notes/edit/{id}`: load existing note and its ordered blocks.
- Skyline `+` → **ثبت یادداشت جدید**, and **دانش‌نامه توسعه → یادداشت‌ها** in the sidebar.
- `AccessKeys.Notes` = `wiki.notes.view`: route guard, menu visibility and server-side checks.
- Any user with access can read notes. A note may be edited or deleted by its author or an Admin/Owner, following existing cURL/SQL behavior. Only Admin/Owner can assign another author.

## Editor behavior

- Click the centered **+** above or between blocks to add **عنوان فرعی**, **پاراگراف**, or **قطعه کد**.
- Heading and paragraph inputs share reusable `NoteTextBox` / `NoteTextArea`; code uses `NoteSnippetBox` with optional language label.
- Use ↑ / ↓ to reorder blocks and × to remove one. Blank blocks are not saved.
- `NoteContent` displays persisted blocks as plain text (HTML-escaped by Razor); code stays inside an unexecuted dark code panel.
- Saving and canceling require the existing `AppModal` confirmation; updates use the existing busy overlay and toast notifications.
- A title is the **only required content**; empty body notes are allowed.

## Storage and boundaries

- `Note`: title, Identity `UserId`, summary excerpt, created/updated timestamps and soft-delete state.
- `NoteBlock`: NoteId, zero-based position, Heading/Paragraph/Code kind, text and optional code language.
- The list query retrieves note summaries only, not full code blocks.
- Server validation limits title (250 characters), block count (120), per-block length, total content length and code language length.
- Deleting a note soft-deletes its parent while preserving the content internally; block replacement occurs in EF Core SaveChanges.

## Local test checklist

1. Build project, apply migration, sign in as User, Admin and Owner.
2. Open notes from sidebar, quick search and Skyline +.
3. Create an empty title (expect validation); then create a title-only note.
4. Create a note with two paragraphs, headings and code; reorder and save, reopen to verify exact order/content.
5. Change code language, add/remove sections, save, refresh and preview.
6. Verify normal users cannot assign another author or edit/delete someone else's note—even via a forged service request.
7. Verify Admin/Owner can select author, edit and delete existing notes.
8. Remove `wiki.notes.view` from a role, confirm route and service access is denied.
9. Confirm existing chat and cURL/SQL functionality still works.

**Verification note:** This packaging environment has no .NET SDK/SQL Server, so a compiled build, migration execution and browser end-to-end testing must be performed locally.
