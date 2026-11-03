# Glasswork 2.6 — Chat Notifications, Typing & Workspace Polish

Based on **Glasswork 2.5**. This release changes **no database schema** and preserves all previous migrations and encrypted message storage.

## Features

- **Skyline:** consistent chat SVG and unread-message count badge (`99+` cap). Unread counts are derived from the current member's read state, excluding the member's own messages.
- **Always-on chat connection:** `ChatInboxHost.razor` is mounted in `MainLayout.razor` and owns a single SignalR WebSocket, even when the chat modal is closed. The modal consumes shared `ChatInboxState` events rather than opening a second socket. Unread count refreshes at login, reconnect, room changes and new-message/read events.
- **Unified notifications:** incoming messages appear in the existing `AppNoticeService` / `AppNoticeHost` at the bottom-left. Older notifications contract into one-line rows above the newest expanded message, sharing one stacked glass surface. The stack holds up to six notifications, each with its own expiration. Clicking the open icon on a chat notification opens its room; ordinary success/error notices continue to work as before.
- **Typing indicators:** `ChatHub.SetTyping(roomId,isTyping)` validates authenticated room membership and broadcasts ephemeral `ChatTyping` events only to the *other* room members. The browser throttles typing signals and sends a stop event after inactivity. A short-lived indicator (group rooms only) shows participants' display names and animated dots. No typing data is persisted.
- **Empty chat view:** custom CENOPS-style illustration, strong accent colors, creating a conversation and searching colleagues, with a privacy note.

## New/changed important files

- `Components/Chat/ChatInboxHost.razor`, `ChatInboxState.cs` — new app-wide listener and state.
- `Components/Chat/ChatWorkspace.razor` / `ChatWorkspaceState.cs` — uses the shared connection; shows typing; supports room-open requests.
- `Chat/ChatHub.cs` — authenticated, membership-checked typing.
- `Components/Common/Notices/AppNoticeService.cs`, `AppNoticeHost.razor`, `AppNoticeMessage.razor` — reusable stacked notification system.
- `Components/Layout/MainLayout.razor` and `Skyline.razor` — app-wide host and unread badge.
- `wwwroot/js/chat-realtime.js` — one persistent WebSocket, message payload callback and typing sender.
- `wwwroot/css/chat-workspace.css`, `cenops-feedback.css` — UI styling.

## Operational scope

Notifications work while the user is logged in and the **CENOPS web application remains open**, even if the chat modal is closed. This is **in-app real-time notification**, not an operating-system/browser push subscription after the browser closes. No service worker or external push service is included.

This chat listener uses a WebSocket connection to `/hubs/chat`; configure WebSocket support on the reverse proxy. The data store remains authoritative: disconnects are resynced on reconnect. Typing status is best-effort and automatically times out.

## Local verification checklist

1. Run `dotnet build` at the solution root. The build tool was not available in the packaging environment.
2. Log in as two users in separate browsers/tabs. Keep A on Dashboard; have B send a DM. A's Skyline count should increase and an existing-style toast should appear at bottom-left without opening chat.
3. Click the chat toast's open arrow. It should open the chat modal directly into that room, and reading should clear the badge count.
4. Send several messages rapidly. Confirm latest notification is expanded, older ones are compact above, and separate success/error toasts also stack.
5. Open the same group with both users; type, stop, then leave. Confirm the other user sees the typing indicator and it disappears after a few seconds.
6. Verify a third non-member receives **no** typing events or message notifications for the room.
7. Close the chat modal, navigate across the app, then repeat the notification test: only one real-time connection should remain active per browser circuit.
8. Confirm the new empty view and badge in mobile/responsive layouts.

No additional `dotnet ef database update` is needed for this release if the 2.5 migrations are already applied.
