# Glasswork 2.29 — Calendar month UX and note editor layout

Baseline: Glasswork 2.28. All changes are additive or targeted to existing components.

## Calendar and event deletion

- Monthly cells show up to two event previews and a `+ N` overflow indicator; **there is no scroll within a day cell**. Select a cell to open an accessible day-events modal with times, authors (existing `UserHoverName`) and a Details action for each occurrence. Clicking Details transitions to the existing event-details modal. Monthly view itself can scroll if six calendar rows exceed the viewport.
- Event details and the event editor use the shared `AppModal` confirmation pattern and consistent `.knowledge-button` action styles for Cancel/Delete. The event editor's destructive button is the same 36px control height as its neighboring controls. The events DataTable delete confirmation uses those same shared action styles. Server-side delete authorization remains unchanged.

## Note/todo insertion affordances

- Add controls are hidden by default. Hovering the gap, reaching a block's upper/lower edge, or keyboard-focusing the control reveals the corresponding insertion affordance. Empty editors keep their initial plus control visible so the first item is discoverable.
- Existing Todo editor markup and its keyboard shortcuts remain intact; the shared delegated drag/hover script still uses its original `MoveBlockAsync` path there.

## Horizontal note rows

- Note blocks have an optional `RowGroup: Guid?` property. `null` means a full-width row; consecutive blocks sharing a non-null group appear side by side, with equal columns and responsive single-column presentation on narrow screens.
- Hover a note block's right or left edge to insert a Heading, Paragraph, Code or Table block beside it. Up to four blocks can share a row.
- Drag by the existing handle. Dropping at the left/right edge adds the block to that row; dropping above/below puts it into a standalone full-width row. Drag DOM behavior is delegated to the existing `cenopsNoteDrag` script; its `data-note-spatial` flag activates the new pathway without modifying Todo drag semantics.
- Saved note preview (`NoteContent`) uses the same row grouping; historical blocks with no group keep their full-width layout.

## Headerless tables

- New `NoteBlockKind.Table = 4`, preserving existing numeric values.
- `NoteTableData` is a headerless list of rows of plain-text cells, serialized into the existing `NoteBlock.Content` JSON string. No HTML is persisted or rendered. Shape is validated by the application service: 1–20 rows, 1–12 columns, all rows equal width, 2000 characters maximum per cell.
- Reusable `NoteTableEditor` and `NoteTableView` components. Hover a horizontal or vertical boundary (including the outer edges) to insert a row or column at that exact position. Hover a cell for the row (right edge) and column (top edge) delete controls. Empty rows/columns delete immediately; nonempty rows/columns require the shared `AppModal` confirmation. A table retains at least one row and one column.
- Table text is bound on change/blur to avoid replacing the active textarea value on every key stroke.

## Database migration — required

Migration `20261017143000_AddNoteInlineRows` adds only a nullable `uniqueidentifier` column `RowGroup` to `NoteBlocks`. This is backwards-compatible for existing notes. The table content itself does not require a new table or column.

From solution root, after backing up the SQL database:

```sh
dotnet build
dotnet ef database update --project src/HealthMonitoringApp.Infrastructure --startup-project src/HealthMonitoringApp.WebUI
```

Do not apply the migration until the application builds successfully in your local environment.

## Local testing checklist (not claimed to have run)

1. Compile the full solution and apply the migration. Open an existing note: every block should be full width.
2. Create a note with a Heading, Paragraph, Code and Table; save, reopen, and preview it.
3. Use side + to form two/three/four columns. Drag a block between left/right, then above/below the row. Save, reopen, verify order and column grouping.
4. With a Persian/English paragraph, insert and remove columns/rows at the first, middle and last boundary. Check that empty removals are immediate, but nonempty removals require confirmation. Confirm Escape/cancel leaves table data unchanged.
5. Hover the top and bottom halves of note/todo items. Use keyboard focus to reveal insertion. Test both empty editors.
6. In monthly calendar view, choose a day with no, one, and many events. No nested cell scrollbars; day list shows all and opens details. Daily/weekly views remain unchanged.
7. Try delete from the calendar details, event editor and event list. Confirm button sizing and consistent modals, and verify permission checks across owner/author/other users.
8. Test note table and side-by-side rows at desktop and mobile widths, plus cancel/save behavior.

## Verification performed in this package environment

- Node syntax checks for modified JavaScript.
- Mocked delegated-drag test of left/right/above/below drops and event-listener cleanup.
- CSS parser and project XML checks.
- ZIP integrity and changed-file diff against the original 2.28 archive.

A real .NET/Razor build, EF migration execution and authenticated browser/SQL integration tests **were not run** because this environment has no .NET SDK or SQL Server.
