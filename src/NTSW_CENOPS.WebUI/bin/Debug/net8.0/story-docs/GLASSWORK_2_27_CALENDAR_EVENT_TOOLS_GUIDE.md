# Glasswork 2.27 — Calendar overlap, event management and developer tools

**Baseline:** `NTSW_CENOPS_Glasswork_2_26.zip`  
**Database migration:** None; the existing `CalendarEvents` table and soft-delete flag are reused.  
**Runtime note:** Source-level checks passed, but no real .NET SDK, SQL Server, or interactive Blazor runtime was available in this environment.

## 1. Calendar overlap layout

`Components/Team/TimedEventLayout.cs` is the new pure, reusable layout helper. Each visible occurrence is clipped to its day. Display intervals have a 25-pixel minimum so very short events remain clickable. The helper orders intervals by start time, groups connected overlaps, and greedily assigns the first available horizontal lane. The lane count is calculated **per group**, not for the whole day; events with no conflict use full width. Placement includes invariant-culture pixel measurements and percentage lane widths. Weekly and daily Razor templates use the same helper.

Monthly cells now allow vertical scrolling and render all events instead of cutting off after three. The full-year view is unchanged. Calendar page bottom padding is owned by the shell (20px desktop, 14px narrow screens), rather than relying on child margins inside an overflow-clipped article.

## 2. Event list and deletion

Added the permission-protected route `/team/events/list` using the existing `DataTable`, `DataTableToolbar`, `DataTableController`, and `DataTableFilterMatcher`. It includes pagination, filters, Jalali display-only timestamps, and custom New/Edit/Delete actions matching the report-management workflow. A new `ListAsync` method on `ICalendarEventService` returns active event definitions (not materialized recurrence instances or birthdays). The service joins the author while filtering out soft-deleted events.

The event details dialog in the calendar now has Delete with a separate confirmation dialog; the calendar reloads after success. Both the event list and the calendar use the **existing** `CalendarEventService.DeleteAsync`, which rechecks edit/deletion rights and soft-deletes the event. The UI also restricts the list buttons to the event creator or Admin/Owner. Birthday virtual occurrences remain noneditable and undeletable.

## 3. Loading components

Replaced several ad hoc inline waiting messages with the reusable `AppLoading` (`Variant="inline"`) where only a panel is waiting: calendar, event editor, report manager/editor/gallery, dashboard home, smart-card loader/configuration, and dashboard content browser/preview. Existing transient save/delete button state labels are preserved for accessible feedback.

## 4. Code snippet language detection and formatting

`NoteSnippetBox` is still the single editor used by the wiki note editor, administration, and cleanup tool. It now offers an explicit `cURL` language option and status feedback for the built-in format action. Automatic detection recognizes valid/escaped JSON, SQL entry statements and CTEs, cURL commands, and the preexisting language heuristics; a manual language selection is respected while editing.

`NoteCodeFormatter` now separates SQL clauses without processing SQL comments, quoted strings, or bracketed identifiers; formats safe CSS block structure; formats single-line cURL flags while retaining quote/escape contents; and repairs/indents JSON only when it can parse the repaired result. Arbitrary program text is **not** aggressively rewritten where no parser exists; preserving executable behavior is more important than cosmetic indentation. No third-party package, external endpoint, or remote code execution is used.

The redundant formatter button on `/tools/clean-code` has been removed. The snippet box's existing toolbar owns this action. The Save-as recommendation recognizes the explicit cURL type.

## 5. Skyline tools menu

The sidebar Tools icon and the new skyline button share a sliders/control-panel visual language. The skyline button sits to the right of chat/create in the RTL header. It has a visible `CTRL I` hint and opens the same styled dropdown pattern as Create. The initial menu action is **پاک‌سازی کد**, shortcut **C** when the menu is open. The global keyboard handler supports Ctrl/Cmd+I to toggle the menu and Escape to close. Only one skyline popup is open at once, and navigation uses the existing authorized cleanup route.

## Suggested local verification

1. Run `dotnet build` and resolve any real Razor/compiler errors before running migrations. No migration was added.
2. Create overlapping events at 09:00–10:00 and 09:20–09:40, plus a third at 09:45–10:30. Check they occupy separate lanes in the daily and weekly views, and that their tooltips/details remain clickable.
3. Check two simultaneous short events, multi-day events crossing midnight, and a day with 20+ events in the monthly cell. Month cells should scroll without painting events on top of one another.
4. Delete an event from the calendar details dialog; verify confirmation, immediate disappearance, and absence from the event list after reload. Check the same operation in the DataTable. Test both creator and unauthorized viewer accounts; backend deletion must reject the latter.
5. Filter and paginate event rows, then use custom New/Edit buttons. Birthdays should remain in calendar occurrences, not in this database-definition list.
6. Paste normal JSON, double-encoded JSON, escaped object JSON, a multi-clause SQL statement with quoted strings and comments, and `curl --data '{"key":"value"}' ...`. Verify language selection, preservation of quoted content, and toolbar formatting behavior.
7. Test `Ctrl+I` from the main shell, `C` inside the tools menu, Escape dismissal, and interaction with Create (`Ctrl+B`) and Chat (`Ctrl+M`). The tools menu should not steal input while typing.
8. Test at desktop and mobile widths; verify the calendar has a visible bottom gutter and no outer scroll while its view can scroll.

## Build validation performed

- All JavaScript files passed `node --check`.
- All CSS files passed `tinycss2` stylesheet parsing.
- All `.csproj` files parsed as XML.
- Targeted source assertions verified new routes, helper, service wiring, and keyboard hooks.
- Both release archives pass ZIP integrity checks once packaged.

**Not claimed:** real .NET compilation, Entity Framework/SQL Server integration, and browser-based Blazor interaction tests. These must be performed in the local development environment.
