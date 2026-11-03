# C.E.N.O.P.S. 2.66 — GLASSWORK
## Chat attachments and parent-scoped file privacy

### Release scope
- Chat composer has a borderless attachment button next to Send. It opens the existing `AppFileBlock` in **Edit** mode inside `AppModal`. Uploaded attachments are retained in the compose draft until Send.
- Text-only, file-only, and mixed messages are supported. One attachment is displayed inline as an image, video (`.mp4`/`.webm`), or a compact document card. Multiple attachments reuse `AppFileBlock` in **View** mode with its existing list/gallery and pagination.
- A single large Chat file preview modal handles double-clicks from the multi-file component and file-card/image clicks. PDF and text/Office previews reuse `AppFilePreview` and the existing FileStreaming preview reader.
- `ChatMessageAttachment` has a relational key (`MessageId`, `FileId`), ordered `Position`, and an index on `FileId`. New EF migration: `20261102160000_AddChatMessageAttachments`.
- Chat History loads attachments **in one batch** for the current page, rather than N+1 queries; forwards preserve links, edits preserve links, and soft-deleted messages no longer authorize file delivery.

### Authorization: do not trust parent IDs alone

Example authorized URLs:

```
GET /api/files/{fileId}/content?messageId=120
GET /api/files/{fileId}/download?noteId=42
GET /api/files/{fileId}/preview?noteId=42
```

All three routes require an authenticated session and invoke **one** `IFileResourceAccess` policy. It denies access for missing IDs, both parent IDs, invalid IDs and unauthorized associations. The parent ID is a *lookup key*, not a capability or password.

- **Chat:** verify Chat permission, current room membership, non-deleted message and the requested file's actual `ChatMessageAttachment` record for that message.
- **Notes:** reuse `INoteService.FindAsync` for the application's existing public/private, role and owner permissions; verify the file ID exists in a persisted File block of that Note.
- **Upload ownership:** FileStreaming stores an opaque `OwnerKey` in metadata. The reusable Blazor upload block reads the authenticated user ID from the server-side authentication state. Chat sending and Note saving verify that a **new** file reference belongs to its uploader, preventing cross-parent exposure through forged file IDs. Legacy files already saved in a Note remain valid when editing that Note.
- **Physical deletion:** freshly uploaded files can be physically deleted by their uploader only while they have **no persisted Chat/Note references**; otherwise removal only detaches them from the draft. Server-owned paths are never mounted under `wwwroot`.

All requests denied by the resource policy return **404**, to avoid revealing whether the file exists. The app-facing storage contract remains `IAppFileGateway`; FileStreaming is still referenced only from Infrastructure.

### Why a separate message/file relationship?

Storing file IDs in separate relational rows allows efficient membership checks without parsing message text and supports forwarding or revoking individual message-based access. Unlike Notes' File blocks, Chat content cannot be trusted as an authorization record by itself. SQL relationship writes happen in the same transaction as message creation or forwarding.

### Intentional constraints and follow-up work

- Maximum file size remains **25 MiB per file** by default. `.mp4` and `.webm` were added to the allowable extensions; adjust deployment configuration if larger videos are desired.
- Malware scanning, compression/transcoding, deduplication and full attachment retention are later roadmap phases. Don't use unscanned uploads for highly sensitive environments yet.
- **Before a Note or Chat message has been saved**, no `noteId`/`messageId` exists. Consequently, draft files have no public content URL and cannot be previewed through HTTP until attached. The draft editor still shows filenames and upload state. This intentionally follows the rule that URL requests without a parent are denied.
- Abandoned drafts can leave orphaned files. Implement expiration, cleanup and upload lifecycle states later. There is a possible upload/delete race between FileStreaming metadata and the SQL attachment transaction; it should be addressed when lifecycle management is introduced.
- The current metadata persistence uses the existing JSON FileStreaming store. `OwnerKey` is backward compatible with legacy entries that have no ownership field; these older files can still be read via their persisted Note relationship, but cannot be newly attached elsewhere without re-upload.

### Local build and migration

```
dotnet build NTSW_CENOPS.sln

dotnet ef database update \
  --project src/NTSW_CENOPS.Infrastructure \
  --startup-project src/NTSW_CENOPS.WebUI
```

Migrate the SQL Server database **before** testing Chat messages. For manual local testing:

1. Upload PNG, WEBM, PDF and DOCX to Chat; test file-only and mixed text/file messages, single and multiple attachments and Send/Forward.
2. Open an authorized link in the browser with `messageId`: authorized room members may retrieve it.
3. Remove that user from the room: retry the same URL and expect 404.
4. Try only the file UUID, an unrelated `messageId`, a foreign `noteId`, or both IDs: all must fail.
5. Try a public Note, a private Note as owner, and a private Note as another user. Verify the existing Note permissions.
6. Delete the Chat message or Note: its associated file URLs must stop working.
7. Cancel an upload modal and re-open to verify the draft's already-uploaded file IDs remain; unsaved file handles may be discarded and orphan cleanup is future work.

### Testing performed here
- New `tests/ChatAttachment266/test_chat_attachment_266.py`: source-level ownership/authorization/attachment wiring and Chromium overlay position.
- Existing file-block, FileStreaming boundary, file-preview, graph, SQL-generation and About regression scripts may be run locally.
- **Not performed in this environment:** .NET compilation, EF migration execution, real SQL Server integration or end-to-end authenticated SignalR interactions. These require the local SDK/database.
