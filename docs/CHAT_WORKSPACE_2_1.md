# Glasswork 2.1 — Chat Workspace

This version is based on **Glasswork 2.0 REBASED** (the version built on the uploaded, customized `project_1_9.zip`). Only chat workspace and Skyline/layout assets were modified; the 2.0 chat DB migration, authorizations, Identity configuration, and other UI remain unchanged.

## Features

- Skyline: the old static two-option workspace tab is removed. A small chat icon beside the create (+) button opens the workspace.
- Full-screen window overlay: dark custom CENOPS design with macOS red/yellow/green controls; red closes, yellow minimizes to a persistent floating restore tab, green maximizes/returns to the normal large frame. Overlay animates in; underlying operations route remains in place.
- Left project/navigation rail; list of groups and direct rooms (Discord-style `#` names), unread counts and room filter; current user's display name and username at bottom.
- Room creation from usernames. Single other username creates/opens an existing DM; 2+ others create a distinct group. Group requires a title. Room participants are presented by their **public usernames**, never their internal user IDs.
- Open room, fetch latest 50 messages, load older 50 at a time, see member names in a slide-over panel, search within *loaded* messages.
- Non-bubbled Discord-like message rows, right-click menu (accessible ellipsis alternative on hover) for reply, forward, edit (own message), delete (own message, with confirmation). Initial reply/forward are **quoted text**, not structured reply/forward database records.
- Text composer autosizes up to 180px. Enter sends; Shift+Enter adds a newline. `dir=auto` supports Persian and English. Attachments/voice are intentionally not included.
- Server's `IChatRoomService` and `IChatMessageService` are called inside the authenticated Blazor circuit, so all existing room-membership and author-only checks still apply.
- Incoming messages and room updates are streamed from the **existing authenticated SignalR hub**. A small native WebSocket + SignalR JSON protocol receiver in `wwwroot/js/chat-realtime.js` avoids extra NuGet or CDN dependencies. It reconnects automatically and refreshes SQL-backed history after reconnect. This receiver **requires WebSockets** at the web server and any intermediary reverse proxy. Unlike the full Microsoft JS client, it has no SSE/long-poll fallback.

## Important privacy rule

**Only members** can load or send room messages. There is **no Admin/Owner exception**. Sender identity is derived server-side; usernames identify recipients.

## Files changed / added

- `src/HealthMonitoringApp.WebUI/Components/Chat/ChatWorkspaceState.cs` (new)
- `src/HealthMonitoringApp.WebUI/Components/Chat/ChatWorkspaceHost.razor` (new)
- `src/HealthMonitoringApp.WebUI/Components/Chat/ChatWorkspace.razor` (new)
- `src/HealthMonitoringApp.WebUI/wwwroot/css/chat-workspace.css` (new)
- `src/HealthMonitoringApp.WebUI/wwwroot/js/chat-realtime.js` (new)
- `src/HealthMonitoringApp.WebUI/Components/Layout/Skyline.razor`
- `src/HealthMonitoringApp.WebUI/Components/Layout/MainLayout.razor`
- `src/HealthMonitoringApp.WebUI/Components/App.razor`
- `src/HealthMonitoringApp.WebUI/Program.cs`
- `src/HealthMonitoringApp.WebUI/wwwroot/css/operations-shell.css`
- `src/HealthMonitoringApp.WebUI/wwwroot/css/operations-refinements.css`

## Setup

The existing **2.0 chat database migration** must already be applied and Data Protection key storage configured as described in `CHAT_BACKEND_2_0.md`. **No new migration** is required for 2.1.

```powershell
dotnet restore
dotnet build
dotnet ef database update --project src/HealthMonitoringApp.Infrastructure --startup-project src/HealthMonitoringApp.WebUI
```

Ensure HTTPS/WSS and WebSocket support for `/hubs/chat` in IIS, nginx or your reverse proxy. The same-origin Identity cookie authenticates that WebSocket connection.

## Manual acceptance test

1. Sign in as user A, click chat icon next to + and confirm fullscreen overlay. Close / reopen, minimize / restore. Test navigation behind a minimized chat.
2. Create a direct chat with username B; repeat and verify no second DM was created.
3. Create two groups with the same 3+ members; verify separate room IDs.
4. Open a room in separate browser sessions A and B; send a message and confirm instant appearance in the other session. Disable network briefly to test reconnect and history synchronization.
5. Verify group members are displayed in the right-side slide-over and that an unrelated third user (even Owner/Admin) cannot access the room.
6. Send multiline Persian and English text. Enter sends; Shift+Enter inserts a newline. Confirm that the input expands naturally.
7. Right click your own message: edit, delete (with confirmation), reply, and forward. Right click someone else's: reply and forward only. Deleted text should not render.
8. Test room search and message search. Note that message search is **limited to currently loaded messages**; full-history server-side search is not part of the 2.1 basic UI.
9. Confirm the existing DataTable, Skyline + menu, knowledge pages and access management still work.

## Limits reserved for later versions

No file/voice, no actual user presence service (the green self-indicator means **your realtime connection**, not another user's online status), no separate message reactions, no typed reply/forward relationships in SQL, no message search over all history, no mobile full-screen route. The overlay's live connection persists while minimized but closes when the window is fully closed. These are intentional scope limits, not simulated capabilities.

## Validation limits

Static checks and JS/CSS syntax validation were run in the packaging environment. `dotnet` was unavailable, so an actual build, browser test and multi-user integration test still need to be performed locally.
