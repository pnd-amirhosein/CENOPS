# Glasswork 2.5 — Message interactions

Based on **Glasswork 2.4** (including the customized/rebased original project). Only chat-related source files were changed.

## Changes

- Composer has exactly one visible border on its container. Textarea and textarea focus have no additional border or outline; focus styling changes the outer border.
- Removed message-hover ellipsis. Right-click opens a compact, icon-based, RTL context menu, clamped to the chat modal's own rectangle (not the browser viewport).
- Deleted messages are excluded from message history and rendering. They also remain excluded from global search. Replies whose source message was deleted do not render a placeholder preview.
- Telegram-inspired reply and edit banner appears above the composer, with a preview and close action.
- Reply relationships are now **structured** (`ReplyToMessageId`) and survive refresh. Reply content preview is resolved from the user's authorized room history; clicking the excerpt or arrow jumps/highlights the original message, including older messages.
- Forwarding is now a **server-side action**, not client-authored quoted text. The original sender's display name and username are saved as forward metadata. Both the source and destination room require membership; there is no Owner/Admin exception.
- User display names in messages, forwarded references, and group-member lists are clickable to open or create a DM, using the existing username-based room service.
- Forward destination selector uses the native CENOPS quick-search visual language, filters by room title, member username or display name, and also searches users. It previews the latest non-deleted message of existing rooms (bounded to the first 25 rooms).
- Updated chat stylesheet cache key to `2.5.0`.

## Database update required

A **new additive EF Core migration** is included:

`20261011145000_ChatMessageReferences`

It adds three nullable columns to `ChatMessages`:

- `ReplyToMessageId` (bigint, nullable)
- `ForwardedFromDisplayName` (nvarchar(180), nullable)
- `ForwardedFromUsername` (nvarchar(256), nullable)

Existing messages and encrypted content are preserved. There is no destructive data migration.

From the solution root, after taking a database backup:

```powershell
dotnet ef database update --project src/HealthMonitoringApp.Infrastructure --startup-project src/HealthMonitoringApp.WebUI
```

Ensure your database already has the **re-based `Initial`** and Glasswork 2.0 chat migration recorded in `__EFMigrationsHistory` before applying this update.

## Local verification checklist

1. Run `dotnet build NTSW_CENOPS.sln` and apply the new migration.
2. Open chat and verify only the outer composer border highlights on focus.
3. Open a message context menu at every corner of the modal; it should remain fully visible.
4. Send a reply, reload, click its excerpt; the original message should scroll into view and highlight.
5. Edit a message; verify the edit preview appears, then disappears when canceled or saved.
6. Forward to an existing room and to a user without a DM; check display name and destination history.
7. Delete a message; verify it never renders as a tombstone or placeholder, including after reload.
8. Open a DM from a group sender, forwarded author, or member panel; normal member authorization must still apply.
9. Ensure global search cannot surface deleted messages or messages outside joined rooms.

**Validation note:** The package was checked statically and ZIP integrity was verified; a .NET SDK/browser was unavailable here, so a compile and two-user UI test remain necessary.
