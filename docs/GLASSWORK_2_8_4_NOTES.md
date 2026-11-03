# Glasswork 2.8.4 — Keyboard Shortcuts and Chat Chords

Base: **Glasswork 2.8.3**. This is a UI/keyboard-only update; there are no EF or service changes.

## App-wide Skyline shortcuts (when chat is closed)

| Shortcut | Action |
|---|---|
| `Ctrl+B` (or `Cmd+B`) | Open the existing permission-checked Skyline **+** menu |
| `Ctrl+M` (or `Cmd+M`) | Open chat via the existing `chat.use`-checked handler |
| `Ctrl+K` (or `Cmd+K`) | Open the existing Ctrl+K application quick-search dialog |
| `N`, `C`, `S` with the Skyline + menu open | Create Note, cURL, or SQL via the existing access-checked menu items |

The Skyline create and chat buttons now show `CTRL B` and `CTRL M` key hints in the existing visual style. Chrome's former `Ctrl+N` shortcut and `Alt+N` fallback are removed. Browser-reserved shortcuts are always subject to browser/platform handling.

## Chat modal shortcuts

Chat has keyboard priority while `.cenops-chat-window` is in the DOM:

1. Press `Ctrl+K` (or `Cmd+K`) to **arm the chat keyboard chord**.
2. Press `S` to open the existing chat-wide Search view; press `N` to open the existing New Chat dialog.
3. `Esc` cancels the chord. A different printable key cancels it; it also expires after five seconds.
4. When armed, small `N` and `S` kbd hints appear beside the respective chat-rail buttons. They're absolutely positioned and take up no layout space.
5. While chat is open, `Ctrl+B`, `Ctrl+M`, and the background app's `Ctrl+K` are blocked from activating background UI. If a Skyline creation menu was open before chat, its `N`/`C`/`S` keys cannot navigate away.
6. Native chat typing, input interactions, normal message commands, and chat's existing permission/member checks are unchanged.

## Security

The handlers click the **same real Blazor buttons** as mouse actions. Skyline creation checks current access on menu open **and again before navigation**; chat checks `chat.use` on open, and the workspace is also rendered behind `AccessGate`. Keyboard shortcuts do not bypass access controls. This patch does not change the authorization implementation.

## Changed project files

- `src/HealthMonitoringApp.WebUI/wwwroot/js/site.js`
- `src/HealthMonitoringApp.WebUI/Components/Layout/Skyline.razor`
- `src/HealthMonitoringApp.WebUI/Components/Layout/Skyline.razor.css`
- `src/HealthMonitoringApp.WebUI/Components/Chat/ChatWorkspace.razor`
- `src/HealthMonitoringApp.WebUI/wwwroot/css/chat-workspace.css`
- `src/HealthMonitoringApp.WebUI/Components/App.razor` (asset cache versions)

## Validation and local test

- `node --check` on the updated JavaScript.
- Automated simulated keydown/capture tests for app-wide shortcuts, modal keyboard isolation, both chat chords, Esc, and regular typing.
- CSS parsed using tinycss2, no parser errors.
- Both ZIP files pass CRC integrity tests; all other baseline files verified byte-for-byte unchanged.
- **The .NET SDK and a live Blazor browser were unavailable here**. Run `dotnet build`, then verify with Chrome/Edge and two authorized users. No migration required.

