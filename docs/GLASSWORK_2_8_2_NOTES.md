# Glasswork 2.8.2 — Editor controls, library actions and Skyline shortcuts

Based on the **full Glasswork 2.8.1** project. No database changes.

## Notes block controls
- Every block is wrapped by `note-block-row`, a hover/focus target that **includes the complete left gutter**. Both external controls remain visible as the pointer moves from the content to the controls.
- Drag uses the existing `cenopsNoteDrag` delegated HTML5 drag/drop behavior and `MoveBlockAsync`. The wrapper is the drag target; the browser payload is still initialized by `note-drag.js`.
- The delete control now sits **outside** the block beside the drag handle, using a compact SVG trash icon. Both controls remain visible on narrow/touch screens.

## cURL / SQL / Notes card menus
- Consistent, compact edit and delete buttons with inline SVG icons; each uses the existing authorization and confirmation flows.
- Menu colors inherit the library accent: cyan for cURL, purple for SQL, orange for Notes.

## Skyline quick-create
- `Ctrl+N` (or Command+N) tries to open the same `+` menu. Browser-reserved shortcuts (particularly Ctrl+N) **cannot be guaranteed** to be intercepted in all browsers.
- While menu is open: `N` creates a note, `C` creates a cURL, `S` creates a SQL query. `Esc` dismisses the menu.
- Shortcut navigation activates **the rendered permission-gated button itself**; unauthorized items never become accessible via keyboard.
- Keys are displayed in the search-style `<kbd>` badges beside the launcher and menu entries.

## Local testing
1. Hover a note block, move left into the gutter, drag and drop above/below another block. Confirm controls stay visible during the movement.
2. Delete only one block and save/reload; confirm remaining order persists.
3. Open all three libraries, check ellipsis icon and menu on edge cards; verify edit/delete flow.
4. Click Skyline `+` and press N/C/S in turn. Check role-restricted options cannot be invoked.
5. Try Ctrl+N in browser; if the browser opens a new window, use the `+` click (browser takes priority for its own reserved shortcut).

No EF migration required. No chat, notification, or Notes service changes.
