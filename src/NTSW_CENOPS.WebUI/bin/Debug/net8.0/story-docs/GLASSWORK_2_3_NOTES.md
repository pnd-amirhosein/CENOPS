# Glasswork 2.3 — Chat modal shell correction

Based on **Glasswork 2.2**. Focused UI correction only; no database, SignalR protocol, or chat-service changes.

## Changes

- Chat modal is centered at 94vw × 92vh (95vw × 94vh on compact screens).
- Clicking the dimmed/blurred backdrop closes the chat.
- Explicit **light** palette, matching the CENOPS white/glass UI. The chat CSS link has a `?v=2.3.0` cache-busting query parameter.
- Removed old macOS-style top window controls and minimize/restore UI.
- One compact modal header: **چت اسپیس** at the right; a single X close button at the far left.
- Centered SignalR connection status: animated «در حال اتصال...» while disconnected, «متصل شد» briefly on successful connection, fading out automatically. Shows connecting again if the connection drops.
- Existing messaging, room navigation, and Spotlight search remain unchanged.

## Files changed

- `src/HealthMonitoringApp.WebUI/Components/Chat/ChatWorkspace.razor`
- `src/HealthMonitoringApp.WebUI/Components/Chat/ChatWorkspaceState.cs`
- `src/HealthMonitoringApp.WebUI/wwwroot/css/chat-workspace.css`
- `src/HealthMonitoringApp.WebUI/Components/App.razor`

## Local verification

1. Run the app and open chat from Skyline. Verify the entire modal uses the **light** theme, not an old cached dark stylesheet.
2. Check dimensions on desktop and narrow viewports, including the blurred outer margin.
3. Close using either the left X or by clicking outside the modal.
4. Observe connecting text while the chat hub is unavailable, then check that success briefly appears and fades when restored.
5. Confirm existing room selection, sending, search, and member panel work as before.

No EF migration needed. A .NET build and interactive two-browser test are still recommended.
