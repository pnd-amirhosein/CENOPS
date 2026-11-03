# Glasswork 2.26 — Smart cards, event cards, and code tools

Baseline: **Glasswork 2.25**. All edits are isolated to UI/application-service files. No EF migration required.

## What's new

### Smart cards

- The carousel navigation is a dark-gradient hover/focus control with SVG chevrons, compact progress marks, and a current/total indicator. Controls stay visible on touch-only devices.
- The configuration dialog uses the existing note editor's `cenopsNoteDrag.attach` delegated HTML5 drag system. Drag the six-dot handle; a cyan marker appears above/below the destination item. Drop reorders the items, and the order remains in `DashboardCardOptions.Items` when saved.
- Trash actions remain hover/focus-sensitive, with accessible labels. Settings remain a responsive two-column group, collapsing to one column on mobile.
- Smart cards still resolve nested references using the existing permission-aware content service. No nested content or custom settings are copied into any other user's session.

### Calendar and event cards

- The selected event uses semantic detail blocks with start, end, repeat rule, and next scheduled occurrence; the blocks change to two columns only when the card's available width permits.
- Day, week and month event lists display short recurrence badges for recurring event definitions. One additional permission-checked **batched** query fetches the definitions; it does not fetch the event once per occurrence.
- The detailed recurrence description is still available through the original event details dialog.
- The calendar gets bottom spacing inside its fixed-height shell, leaving view-local scrolling intact and preventing an extra outer scrollbar.

### Tools → Code cleanup

- New **ابزارها → پاک‌سازی کد** route: `/tools/clean-code`, for signed-in users. No table, backend save, or migration.
- Reuses the existing `NoteSnippetBox` for syntax highlighting, language selection and cleanup. The shared `NoteCodeFormatter.Clean` handles:
  - ordinary JSON (valid structured documents),
  - JSON objects/arrays double-encoded as JSON strings,
  - bare escaped JSON copied from logs (only after successful reparsing),
  - SQL clause formatting, and
  - multi-flag cURL formatting, without modifying quoted bodies.
- For other languages it normalizes line endings and removes trailing whitespace; it is intentionally **not** an AST-based code formatter. Python indentation and meaningful shell/SQL escape characters are preserved.
- The top-left **ذخیره به‌عنوان…** action opens three visual options: Note, SQL and cURL. A border/label recommends the appropriate destination according to detected language; all three remain selectable.
- **No automatic save:** the chosen destination route opens its existing *new* editor, prefilled with the code, and standard user permissions, validation and save confirmations still apply.

### Prefilling new editors without URL-encoded content

- `wwwroot/js/snippet-draft.js` stores a one-time draft in **sessionStorage** for a single tab (ten-minute validity, maximum 50,000 characters). A random UUID, not content, is passed as `?draft=<uuid>`.
- `SnippetDraft` DTO is consumed in `OnAfterRenderAsync` by the new Note, SQL and cURL editors. Existing edit routes (`ItemId > 0`) **never** consume or overwrite a draft.
- For notes, the `BlockKind` property supports `heading`, `paragraph`/`text`, and `code`, with language on code blocks. The cleanup-page Save-as operation currently uses `code`.
- Drafts are removed from sessionStorage on consumption. No secret snippet is placed in the URL, backend session or database by this feature.

## Updated interfaces

- `ICalendarEventService.GetRecurringAsync(IReadOnlyCollection<int> ids, ClaimsPrincipal actor, CancellationToken ct)` is a permission-aware batched metadata lookup returning only active normal recurring definitions.
- `CalendarRecurrenceLabel.Brief(CalendarEvent)` produces compact labels; the existing `Get` method still gives the complete formula.
- `NoteCodeFormatter.Clean(content, language)` is a reusable presentation/editor helper, while the existing `Format` and `Detect` API remain available.

## Local test checklist

1. **Compile:** `dotnet build` at solution root. Run existing integration tests, if available. This package has only undergone static checks; full .NET build is not available in the authoring environment.
2. Open a saved dashboard with **two smart cards**. Configure each independently, reorder 3+ items using the six-dot handles, save/reload the dashboard, and verify the order and interval persist. Check both horizontal and vertical navigation, hover/focus, and mobile layouts.
3. Resize a single-event card narrow and wide. Check that details stack/columnize, and that repeat formulas and details dialogs still work.
4. Create one-time and recurring events, then verify the short badges appear only on recurring entries in the today/week/month dashboard cards. Birthdays remain separate virtual occurrences.
5. Navigate to `/team/events` in each calendar mode. Ensure content-view scrollbars still function but the outer page no longer adds a tiny scroll.
6. Open **Tools → Code cleanup**. Paste JSON `{"x":1}`, escaped JSON `{\"x\":1}`, and double-encoded JSON `"{\"x\":1}"`. Check that they become indented JSON and that escape sequences inside valid JSON values remain valid.
7. Format a multi-flag cURL command containing a quoted JSON body. Confirm the body is not rewritten. Check a SQL query and indentation-sensitive code too.
8. Select each **Save as…** target. Ensure only the newly opened editor receives the code; no record is persisted until its regular Save confirmation. Verify Notes create a code block; SQL/cURL populate the code field. Try the edit route separately to confirm existing records are not overwritten.
9. Deny the user Notes, SQL or cURL access, then try the corresponding transfer. The existing destination `AccessAuthorize` must still block unauthorized editing.
10. Use Escape on the Save-as dialog and confirm focus returns appropriately after closing.

## Deployment

Replace files from the changed-files archive, or use the full project archive. No `dotnet ef database update` is required for version 2.26 alone. Retain prior 2.21/2.22 migrations if not already deployed.
