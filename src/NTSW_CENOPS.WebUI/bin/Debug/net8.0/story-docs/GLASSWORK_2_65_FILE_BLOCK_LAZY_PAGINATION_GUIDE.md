# C.E.N.O.P.S. 2.65 — GLASSWORK
## File block: pagination, lazy content loading and multi-batch upload

### Changes

- **View modes share one reserved canvas height** (`cenops-file-view-canvas`, 330–365px) while the gallery preview uses only the remaining space above its metadata label and strip.
- **List mode uses actual client-side pagination** of the *attachment metadata*: configurable `ListPageSize` (default 7), page-number buttons, Previous/Next controls, and total/page indicator. Only the current page's rows render. Page is clamped after changes and resets when a different saved value loads.
- **Lazy payload retrieval**: loading Note attachments resolves `AppFileInfo` metadata by ID; no file bytes are read in the list. Gallery renders `AppFilePreview` for its active item only (initially the first attachment). Gallery strip renders type tiles instead of `<img>` thumbnails, avoiding multiple image requests. The large modal mounts its own viewer only while open.
- **Double-click to open**: both list entries and gallery thumbnails open the large file modal on double-click; a single gallery click only changes the active in-panel preview. The old gallery corner Preview icon is removed. Keyboard users can press Enter to open the preview.
- **Larger preview modal**: dedicated CSS class `cenops-file-preview-dialog`, width up to 1500px and height up to 92dvh, with internally flexed content and safe overflow for small viewports. No changes to global `AppModal` geometry.
- **Upload queue accumulates across selections**. A new keyed native `InputFile` is mounted for each selection; prior inputs remain mounted **only while their files are queued**. This is important because Blazor browser file handles are tied to their original input element. A single shared drop target routes files to the latest picker. Each selection retains its `MaxBatchSize` check; `MaxQueuedFiles` defaults to 40.
- Successful uploads leave the ready queue while failures and invalid entries stay for retry or removal. The queue uses a proper SVG remove icon and the upload count badge inherits the CENOPS font.

### Code locations

- `src/NTSW_CENOPS.WebUI/Components/Files/AppFileBlock.razor`: reusable editing, gallery/list, selection, paging and modal logic.
- `src/NTSW_CENOPS.WebUI/Components/Files/AppFilePreview.razor`: existing standalone lazy viewer, unchanged.
- `src/NTSW_CENOPS.WebUI/wwwroot/css/cenops-file-block.css`: shared-height canvas, pager, and scoped large-modal sizing.
- `src/NTSW_CENOPS.WebUI/wwwroot/js/file-drop.js`: resolves active native input at drop time rather than holding a stale reference.
- `tests/FileBlock265/test_file_block_265.py`: focused browser/source regression.

### What remains deliberately unchanged

- `IAppFileGateway` is still the single application-facing boundary; no UI dependency on the FileStreaming project was introduced.
- No new security, scanning, compression, binary preview format support or SQL migrations.
- Saved Notes still store a list of file IDs, not file bytes.

### Verification

Chromium checks cover shared canvas height, retained native picker handles, new drop routing, file types in gallery tiles, compact queue, and modal design. Existing preview, upload, About and graph checks, SQL generators and 4,418 graph-route cases passed. Full .NET build and live Blazor upload integration remain **unverified** in this environment (no .NET SDK installed).

### Manual acceptance checks

1. Choose two files; reopen the picker and choose two more. Verify all four remain queued and upload successfully. Repeat with drag-and-drop, then remove one before uploading.
2. Create a Note file block with more than seven files; switch to View, paginate, and verify only the current page renders. Double-click a row to preview; single click should not open it.
3. Switch to Gallery and ensure the first file loads; changing thumbnails loads just that file. Double-click a thumbnail to open the larger modal; single click should not open it.
4. Confirm the gallery and list occupy the same reserved height, and the large preview remains usable on smaller screens.
5. Test invalid and oversized files, retry behavior, and mixed-success batches.
