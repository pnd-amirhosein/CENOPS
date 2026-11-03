# Glasswork 2.8.3 — Skyline Keyboard Shortcuts & Authorization

Based on **Glasswork 2.8.2**. No schema changes or database migrations.

## Changes

- Skyline quick-create button is a single wider control containing `+` and the `CTRL N` keyboard hint. Mouse and keyboard use the same Blazor handler.
- `Ctrl+M` (or `Cmd+M` on platforms where delivered to the page) opens the chat workspace.
- `Ctrl+N` is listened for in the capture phase; `preventDefault()` is called **if the browser delivers the event to the webpage**.
- **Chrome/Edge/browser limitation:** `Ctrl+N` is a browser-reserved New Window shortcut. A normal webpage cannot reliably suppress it. Use **`Alt+N` as a browser-friendly fallback**, or click `+`. On systems where `Alt+N` is captured by a browser/menu/OS, click `+`.
- While the create menu is open, press `N` for a Note, `C` for cURL, or `S` for SQL. Typing into a form field never triggers these second-stage shortcuts; `Esc` closes the menu.
- The Skyline create menu fetches current database access grants every time it opens and renders only allowed destinations. Before navigation, it checks the requested permission again server-side.
- Both the Chat button and `Ctrl+M` route through a fresh `chat.use` authorization check. The chat workspace host also has an `AccessGate` so direct state changes cannot render the workspace to a denied user. Existing chat backend security remains in force.

## Testing checklist

1. Click Skyline `+` and verify the larger button and only granted items appear.
2. Use `Alt+N` to open/close the creation menu in Chrome, then type `N`, `C`, or `S`.
3. Where your browser delivers `Ctrl+N` to pages, verify it opens the menu. In stock Chrome, expect the browser's New Window behavior instead.
4. Use `Ctrl+M` to open the chat and verify the existing chat functionality.
5. Temporarily revoke a user's `chat.use` and `wiki.*.view` permissions. Verify the toolbar shortcuts cannot reach forbidden destinations, including if a permission is revoked while the create menu is already open.
6. Verify `Ctrl+K` search and normal typing into textareas still work as before.

## Verification performed

- JavaScript `node --check` and a mock keyboard event simulation for Ctrl+N, Alt+N, Ctrl+M, menu selection and denied/unavailable menu items.
- CSS parser check, static permission wiring checks, ZIP integrity check.
- A full .NET build and interactive browser test were **not available** in this environment.
