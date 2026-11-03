# Glasswork 2.28 — Calendar, Snippet, DataTable and Skyline Fixes

**Baseline:** Glasswork 2.27. No schema or database migration required.

## Implemented fixes

1. **Dashboard event-calendar route:** The calendar link points to the real `/team/events` page rather than the nonexistent `/team/calendar` route.
2. **DataTable dates:** Read-only `DateTime`, `DateOnly` and `DateTimeOffset` cells render as plain Jalali-formatted text with no clickable picker or form chrome. Only the selected, editable row creates `AppJalaliDatePicker`. Editing retains the underlying Gregorian value, time and (for offsets) original offset.
3. **Daily/weekly calendar events:** Removed the heavy right-border stripe from timeline events; separate soft cyan, purple, amber and mint shades identify neighboring appointments. Birthdays retain warm gold.
4. **Delete action:** Event deletion controls share the standard 36px button height and 13px typography of their neighboring controls.
5. **Smart card loop:** Both ends of the slider include a visual clone. A completed edge transition teleports without animation to the equivalent real slide, removing the reverse sweep when wrapping. The next tick is still normalized when motion is reduced and `transitionend` does not fire. Existing direction and interval settings remain unchanged. Original slide markup is extracted to `DashboardSmartSlide.razor` for readability.
6. **Snippet formatter:** Detects typical C# and HTML/XML examples, formats brace-based code with a stateful string/comment-aware formatter, and tokenizes HTML so nested elements are shown on separate indented lines. JSON, escaped JSON, SQL, CSS and cURL paths are preserved. This is a conservative formatter, not a full compiler-backed rewriter; always review executable snippets after formatting.
7. **Snippet caret fix:** The native textarea is no longer value-bound on every Blazor render. Syntax highlighting can update without replacing the user's caret position or scroll. Initial/prefilled values are synchronized through JS; explicit formatting maps selection through the changed text.
8. **Skyline tools:** The toolbar tools button reuses the exact sidebar icon, sits to the left of Create in RTL order, and uses the shared shortcut-hint styling.
9. **Persian keyboard shortcuts:** Skyline, creation menus, chat chord, and search commands resolve physical `KeyboardEvent.code` first (e.g. `KeyI`, `KeyM`, `KeyC`), with a Persian-character fallback. Escape, arrows, Enter, and other non-letter keys retain their existing behavior; native editing remains protected.

## Recommended local tests

- Build the solution using `dotnet build`.
- Open a dashboard event card; select «تقویم», confirm `/team/events`.
- In DataTable, click ordinary date cells (nothing opens). Start editing the selected row and verify the Jalali picker can open and commit, preserving Gregorian storage.
- In weekly/daily calendars, open overlapping and birthday events; check soft colors, readable text, and lane layout.
- Open an event editor with delete controls and compare its height with adjacent Save/Cancel controls.
- Create a smart card with at least three different content items; let it auto-advance for multiple full cycles in both horizontal and vertical modes; use next/previous across the ends. Test with prefers-reduced-motion enabled.
- Type in the *middle* of a long snippet, allow highlighting and language recognition to rerender, and confirm caret/selection and scroll stay put. Then format code with the caret in the middle and confirm it remains near its intended text.
- Paste JSON, escaped JSON, SQL, one-line cURL, multi-line C#, and compact HTML; verify detection and that Format visibly changes formatting where appropriate. Code formatting is conservative and does not guarantee semantics for every language.
- Switch the OS keyboard to Persian. Verify Ctrl+I opens Tools, C triggers Code Cleanup inside its menu, Ctrl+B opens Create, M opens chat, Ctrl+K opens search, and menu letters work physically. Verify typing into a text field doesn't trigger bare-key menu shortcuts.

## Verification performed here

- Static checks for source wiring, route names, Razor markup, JavaScript syntax, project XML and CSS parsing.
- Small Node.js simulation of caret/selection mapping after external formatting.
- ZIP integrity and changed-file comparison against the original 2.27 archive.

**Not run here:** .NET/Razor compilation, a real interactive Blazor session, EF/SQL integration, and cross-browser visual/keyboard validation. The .NET SDK is unavailable in this environment; the local build and interaction checklist above remain necessary.
