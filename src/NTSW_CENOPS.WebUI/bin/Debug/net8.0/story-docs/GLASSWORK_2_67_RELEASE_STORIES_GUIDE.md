# C.E.N.O.P.S. 2.67 — GLASSWORK

## Release Story / داستانچه انتشار

The Skyline product mark now opens a full-screen **release-story reader** instead of navigating to the dashboard. Each release Markdown document is one permanent story. Previous releases remain readable even after their original Markdown files are removed from a later deployment.

### Discovery and persistence

- At build/publish time, `src/NTSW_CENOPS.WebUI/NTSW_CENOPS.WebUI.csproj` copies `docs/GLASSWORK*.md` into `story-docs/` under the application content/output directory. Only **GLASSWORK release documents** are included; infrastructure/developer setup docs such as `AUTH_SETUP.md` are not public patch notes.
- `ReleaseStoryService` discovers new filenames when authenticated Skyline clients retrieve the story list. `ReleaseStories.SourceFile` has a unique SQL index and the importer uses a semaphore for idempotency. It reads Markdown only for new filenames; already imported content is cached in SQL Server.
- `ReleaseStories` stores filename, title, parsed release version, original file creation time, imported time, and a Markdown **snapshot**. This provides indefinite retention even if the Markdown source disappears later. A modified file of the same filename is *not* silently reimported or resent as a new story: publish a new release filename for the next patch note.
- Stories are ordered by semantic release version (`2.9` before `2.10`), then source file creation date and story ID. The release number takes precedence to remain deterministic when ZIP extraction or deployment changes file timestamps.
- New documents added while the app is running are discovered the next time the story list is fetched. The Skyline unread rim checks on component initialization and when a story is viewed. Reloading the page checks for newly deployed stories.
- `ReleaseStories:DocumentsDirectory` can override the directory. Keep the documented files server-side, **not** in `wwwroot`.

### Read tracking / views

- `ReleaseStoryViews` is an explicit `(StoryId, UserId)` relationship with `ViewedAt`. Its composite primary key ensures at most one receipt per user per story; refreshing or reopening never adds another view.
- The selected story is marked viewed when its content opens, not when the user merely sees the logo. The first unread release opens by default; if all have been read, the most recent story opens.
- The logo gets a cyan rim while **any** release is unread for the signed-in user. When all are read, it has no rim; stories themselves never expire.
- The eye at upper left displays story viewers in a left-side panel. Names reuse `UserHoverName` (same user hover/profile and Chat controls), rather than implementing a second profile widget.

### Navigation and UI

- Close (X) is in the upper-left corner. Use side buttons: **right = next** and **left = previous**; progress segments allow direct selection. Left/right arrow keys and Escape are wired as well.
- Reading is manual; there is **no timer, auto-advance or expiry**. Markdown content scrolls inside the stable modal without navigating the page.
- Markdown is rendered through reusable `StoryMarkdown` + `StoryMarkdownRenderer`: headings, tables, lists, code fences, quotes, links, and basic inline styles. This renderer explicitly HTML-encodes original input and allows only HTTP(S) links so raw HTML/scripts cannot run in the application origin.
- Desktop and narrow/mobile layouts are supported. The viewer uses the existing global About overlay outlet to stay outside Skyline's glass clipping context.

### Required database migration

`20261102180000_AddReleaseStories` adds `ReleaseStories`, `ReleaseStoryViews`, unique source-file and per-user indices, and foreign keys.

Before running the application:

```bash
dotnet build NTSW_CENOPS.sln
dotnet ef database update --project src/NTSW_CENOPS.Infrastructure --startup-project src/NTSW_CENOPS.WebUI
```

**Manual integration checklist:** log in as User A, observe unread rim, open successive patch notes, close and reopen; confirm no duplicated views. Log in as User B, ensure unread remains until User B reads. Open the eye panel, hover a viewer name and verify the existing profile card. Redeploy with an additional `GLASSWORK_*.md` and confirm a new story appears without erasing prior receipts. Remove a previously imported file from deployment and confirm the cached story remains.

### Deliberate limitations

- This release does **not** compile/run a live Blazor or SQL Server session in the authoring environment; a local .NET 8 SDK and database are required for complete verification.
- An already-imported filename is immutable by design. For corrections, create a new filename/version; source updates should not retroactively alter what users have already read.
- Very large documents (> 1 MiB) are skipped with a warning. The reader uses a deliberately restricted Markdown subset, not a full CommonMark engine.
- Viewer lists are available to authenticated users; the release notes themselves are not confidential information.
