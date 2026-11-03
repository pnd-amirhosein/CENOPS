# C.E.N.O.P.S. 2.63 — GLASSWORK: Note file blocks and first file upload UI

## Scope

First integration of the existing on-premises `NTSW_CENOPS.FileStreaming` library. Notes now support a **File** block alongside Heading, Paragraph, Code and Table. The shared `AppFileBlock` component is prepared for reuse by Chat, Wiki, Events and Reports. This is still the **raw-upload phase**, without malware scanning, compression, complete attachment ownership/access controls, or background cleanup.

## Module boundaries — important

```
NoteEditor / NoteContent / other future UI
     ↓
IAppFileGateway (Application.Files)  ← only contract UI or endpoints may use
     ↓
AppFileGateway (Infrastructure.Files)  ← ONLY adapter referencing FileStreaming
     ↓
IFileStreamingService (FileStreaming project)
     ↓
LocalFileStorage + JsonFileMetadataStore (private server directory)
```

`IFileCheckService` is a separate preflight service, called **via** `IAppFileGateway.Check`. It checks name/extension and advertised size before sending bytes. `FileStreaming` independently enforces the maximum byte count as it actually reads the stream, so users cannot evade that limit by lying about size. No UI project or application module references FileStreaming directly. To migrate to a standalone file microservice later, replace the **adapter**, not the UI.

## Note integration

- New `NoteBlockKind.File = 5`, without changes to existing enum values.
- `AppFileBlock Mode=Edit` in NoteEditor, with cloud drop zone, multi-select (20 per batch), preflight flags, narrow staging row, explicit Upload button, scrolling uploaded-icon list and confirmed deletion.
- `AppFileBlock Mode=View` in NoteContent with a compact list and Finder-style gallery (large preview + horizontal thumbnails). The file preview modal offers download. Raster images preview inline; other formats receive a file-type placeholder and download button.
- `@bind-Value` persists **only a JSON array of GUID file IDs**, via `FileReferenceCodec` in Application.Files. Filename, MIME type and byte content are never inserted into Note.Content. The existing NoteBlock.Content database column is reused, so **no migration is needed**.
- File rows are displayed everywhere `NoteContent` is already used, including the notes library and dashboard note previews.
- The note excerpt generator ignores raw attachment JSON.

## File stream vs. IFormFile (learning note)

`IFormFile` describes an HTTP form upload. Here Blazor's `IBrowserFile` provides a stream (`OpenReadStream(maxAllowedSize)`); the stream is passed through `IAppFileGateway.UploadAsync` to `FileStreamingService.UploadAsync`. `LocalFileStorage` reads in bounded chunks, writes an uncommitted temporary file, then atomically publishes the complete object and its metadata. This keeps large binary buffers out of the database and avoids loading whole files into RAM.

```
Blazor InputFile → IBrowserFile.OpenReadStream
                 → IAppFileGateway.UploadAsync(Stream,...)
                 → IFileStreamingService.UploadAsync(FileUploadRequest)
                 → private disk / metadata sidecar
```

Blazor Server's streaming transport is appropriate for initial internal testing; high-volume direct-to-object-storage uploads can come later.

## Basic preflight / limits

Default: 25 MiB per file, 20 files per selection, maximum 40 saved references per block. Supported extensions are configurable via `FileUpload:Extensions`; the default includes raster images, PDF, text, common Office formats, SQL, ZIP and 7z. File size is configurable through `FileUpload:MaximumBytes`; the same value configures the streaming byte limit. FileStorageRoot is configurable through `FileStreaming:RootDirectory` (an **absolute path outside `wwwroot`**). Without one, it defaults to a private `NTSW_CENOPS/file-storage` folder beneath the current OS account's LocalApplicationData directory; explicitly set a persistent path in deployment.

These checks **do not** inspect file signatures, detect malware, compress files, or guarantee that an allowed extension contains safe content. Do not use this stage to store sensitive uploads until the security milestone is implemented.

## Delivery / privacy status

`GET /api/files/{id}/content` renders only common raster images and `GET /api/files/{id}/download` sends the original. Both require an authenticated CENOPS session, are accessed through `IAppFileGateway`, and avoid mapping storage files through static middleware. Downloads use an opaque GUID ID and server-controlled MIME types. A cache-restricting header and `nosniff` are set.

**Not ready for production privacy**: These endpoints currently **do not verify whether a user is authorized to view the parent Note**, and a guessed/shared GUID could be accessed by any signed-in user. Step 4 must implement attachment ownership/parent permission checks, audit logging, revocation and signed/streaming strategies before real confidential files are used.

## Deletion and abandoned drafts

Files uploaded in the current editor and removed from its list can be deleted physically. Previously saved Note files are **detached from the current draft**, not physically deleted immediately: if a user cancels editing, the previous saved Note must continue working. Files orphaned by unsaved drafts, canceled edits, removed blocks or deleted Notes require a lifecycle/garbage-collection service in a later milestone (with reference checks and retention). This trade-off is intentional; do not blindly delete persisted blobs inside a Notepad UI callback.

## Tests performed

- `python tests/FileBlockBrowser/test_file_block.py` — JS native FileList drop bridge, staged queue dimensions, mobile width, FileStreaming dependency isolation and Note block wiring.
- `python tests/FileStreamingRegression/test_source_contracts.py` — solution/project and base storage contract checks.
- Existing JS/Chromium graph, About, SQL-generation and 4,418 graph-route regressions.

**Not verified in this environment:** full .NET build, C# FileStreamingRegression runtime, Blazor + SQL Server end-to-end interaction. On a development host, run `dotnet build NTSW_CENOPS.sln` and `dotnet run --project tests/FileStreamingRegression/FileStreamingRegression.csproj` before production deployment.

## Files of interest

- `src/NTSW_CENOPS.Application/Files/FileContracts.cs`, `FileReferenceCodec.cs`
- `src/NTSW_CENOPS.Infrastructure/Files/AppFileGateway.cs`, `FileCheckService.cs`
- `src/NTSW_CENOPS.WebUI/Components/Files/AppFileBlock.razor`, `FileBlockModes.cs`
- `src/NTSW_CENOPS.WebUI/Endpoints/FileEndpoints.cs`
- `src/NTSW_CENOPS.WebUI/wwwroot/css/cenops-file-block.css`, `js/file-drop.js`
- Note editor/reader/insert menu, Notes model/service, DI, Program and App styles/scripts
