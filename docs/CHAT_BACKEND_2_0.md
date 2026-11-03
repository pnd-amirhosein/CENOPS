# Glasswork 2.0 — Chat backend guide

Backend only: no chat pages, overlay, or browser SignalR client have been added in this version.

**Rebased edition:** Applied to the uploaded `project_1_9.zip` without overwriting its custom UI changes. It preserves `20261008130528_Initial` and adds `20261011103000_AddChatBackend` in `src/HealthMonitoringApp.Infrastructure/Migrations/`. The EF model snapshot was extended in place. Do not copy migrations from the older `Persistence/Migrations` project.

**Migration caution:** This project uses a consolidated `Initial` migration rather than the previous multi-migration history. Before running `database update`, verify that the target database migration history corresponds to this uploaded source. If the database was created using an older migration chain, reconcile `__EFMigrationsHistory`/schema on a backed-up test database first; do not run the consolidated `Initial` on an already-initialized database.


## Install / upgrade

1. Back up the SQL Server database and application configuration.
2. Apply the **included** EF Core migration. From the solution root:

   ```powershell
   dotnet ef database update --project src/HealthMonitoringApp.Infrastructure --startup-project src/HealthMonitoringApp.WebUI
   ```

3. Set a stable key directory for **ASP.NET Core Data Protection** using `Chat:KeyRingPath`. This must be an OS-protected folder **outside disposable deployment directories**. Example `appsettings.Production.json`:

   ```json
   { "Chat": { "KeyRingPath": "D:\\SecureAppData\\NTSW\\ChatKeys" } }
   ```

   Or use environment variable `Chat__KeyRingPath`. Give the application identity read/write access, prevent ordinary users from reading the folder, and back it up safely. **Never delete or rotate away the entire key ring** without migrating previously stored ciphertext: historical messages would no longer decrypt. In a multi-server setup, all nodes need the same protected key ring.
4. Configure HTTPS (already enabled in `Program.cs`). Sign in to the existing Identity system. The chat hub authenticates using the existing login cookie.
5. The migration inserts permission `chat.use` and initially grants it to built-in User/Admin/Owner roles. Owner can change grants using the existing access management feature. Chat permission **does not** override room membership.

## What the model represents

- `ChatRooms`: an immutable `Kind` of Direct or Group, creator and optional title. Direct conversations have a sorted pair of internal Identity IDs and a filtered **unique index** so simultaneous sends can't create two DMs. A Group stays a Group even after membership shrinks.
- `ChatRoomMembers`: primary key `(RoomId, UserId)`, join date and last read message ID.
- `ChatMessages`: creator, room, Data-Protection ciphertext, edit and deletion timestamps. Deletion clears ciphertext but retains the tombstone.
- **Public participant identifiers are unique usernames**, not Identity IDs. Numeric Identity IDs are used only inside the server/database.

## Rules

- All authenticated members with `chat.use` can create rooms and send messages.
- `CreateRoom(new CreateChatRoomRequest(usernames, title))`: `usernames` lists **other** participants. The current authenticated user is added automatically. One other unique user yields the single existing/new DM. Two or more others yields a new group (even if identical member sets already exist). Group titles are required. Limit: 100 participants.
- `GetOrCreateDirect(username)` resolves case-insensitive Identity username, returns existing DM, or creates it.
- `SendMessage({content, roomId})`: sends to an existing room after checking membership.
- `SendMessage({content, username})`: resolves/creates a DM and then sends into it. You must provide **exactly one** of `roomId` or `username`.
- `GetMessages(roomId, beforeId, take)` checks membership and returns older messages in chronological order; max 100 per page (default 50). `beforeId` is exclusive.
- **Only current room members** can see the room, its messages, and realtime events. Being Admin or Owner does **not** bypass membership.
- The original sender alone can edit or soft-delete their own message (including Admin/Owner users). Limit: 5,000 characters.
- The creator of a **group** can add/remove members; any other member may leave. The group creator cannot leave without a future ownership-transfer feature. Direct rooms cannot remove members or be left.
- Users added later can see existing room history. That is the simple initial policy; change it if your organization wants history to begin at join time.

## SignalR contract

Hub: **`/hubs/chat`**. Authenticated clients connect using the application's Identity cookie. The following hub invocation names map to the service methods:

| Hub method | Parameters | Description |
|---|---|---|
| `MyRooms` | none | Up to 100 recent rooms and unread counts |
| `GetRoom` | `roomId` | Full room + members, member-only |
| `CreateRoom` | `{ "usernames": ["sara", "ali"], "title": "تیم توسعه" }` | Creator implicitly included |
| `GetOrCreateDirect` | `"sara"` | Unique DM |
| `RenameGroup` | `roomId, "نام جدید"` | Group creator only |
| `AddMembers` | `roomId, ["reza"]` | Group creator only |
| `RemoveMember` | `roomId, "reza"` | Group creator only |
| `LeaveRoom` | `roomId` | Leave a group |
| `SubscribeRoom` | `roomId` | Membership-checked SignalR group subscription |
| `UnsubscribeRoom` | `roomId` | Leave the transport group, **not** the room |
| `SendMessage` | `{ "content": "سلام", "username": "sara" }` | Auto-resolve DM, returns message with room ID |
| `SendMessage` | `{ "content": "سلام", "roomId": 42 }` | Send to known room |
| `GetMessages` | `roomId, beforeId, take` | Paged history |
| `EditMessage` | `messageId, "اصلاح شده"` | Sender only |
| `DeleteMessage` | `messageId` | Sender only |
| `MarkRead` | `roomId, messageId` | Advances last-read pointer |

Realtime **client event names** (and payloads):

- `ChatRoomUpdated` (`roomId`)
- `ChatMessageCreated` (`ChatMessageDto`)
- `ChatMessageUpdated` (`ChatMessageDto`)
- `ChatMessageDeleted` (`roomId`, `messageId`)
- `ChatReadUpdated` (`roomId`, `username`, `messageId`)

Events are sent via `Clients.Users(...)` **only to users currently listed in ChatRoomMembers**. This also reaches their connected devices when they aren't viewing the room. The sender receives their own events too. Clients should deduplicate by message ID.

### Example usage in C# (future client)

```csharp
var room = await connection.InvokeAsync<ChatRoomDto>("GetOrCreateDirect", "sara");
var sent = await connection.InvokeAsync<ChatMessageDto>(
    "SendMessage", new SendChatMessageRequest("سلام!", Username: "sara"));

// sent.RoomId identifies the auto-created or reused DM.
var history = await connection.InvokeAsync<List<ChatMessageDto>>(
    "GetMessages", sent.RoomId, (long?)null, 50);
```

Use the application's own cookie-based authentication in the browser. `ChatRoomDto`, `ChatMessageDto`, and request types live in `HealthMonitoringApp.Application.Chat`.

## Encryption and operational limits

`EncryptedContent` is encrypted at rest with ASP.NET Core Data Protection (`NTSW.CENOPS.ChatMessages.v1` purpose). The app server decrypts it for **authorized members**; this is **not end-to-end encryption**. Do not expose Data Protection keys or include their directory in web static files. Keep the server OS filesystem ACLs restrictive. Messages still travel as readable plaintext to authorized clients over **HTTPS / WSS**.

SignalR delivers best-effort real-time events; **SQL Server is the source of truth**. After reconnect, clients should load `MyRooms` and `GetMessages` to recover anything missed. There is no push-notification system for disconnected devices, file attachment system, or automatic key rotation strategy in this backend phase.

## Test checklist

1. Migrate and restart. Verify ChatRooms, ChatRoomMembers, ChatMessages, and `chat.use` permission exist.
2. Sign in as `sara`, send by Username `ali`, repeat, and verify both messages share one RoomId.
3. Trigger simultaneous sends from `sara` and `ali`; database unique index must allow only **one** DM.
4. Create two different groups with the same usernames; verify **two** distinct group IDs.
5. Join as nonmember `reza` and try GetRoom/GetMessages/SendMessage/SubscribeRoom; all must be denied, **even if Reza is Owner or Admin**.
6. Verify `ali` can edit/delete Ali's messages but not Sara's.
7. Verify DB only contains ciphertext, not plaintext, and a restart with the **same key ring** still decrypts history.
8. Verify notification event delivery to both members, removal revokes later delivery, and refresh catches missed messages.
9. Verify duplicate and nonexistent usernames, malformed ID, empty or oversize messages, read pointers across rooms, and revoked `chat.use` permissions are rejected.

> No .NET SDK was available in the packaging environment. This version needs a local `dotnet build`, migration, and two-user integration test before deployment.
