# C.E.N.O.P.S. 2.62 — GLASSWORK
## Solution rename and FileStreaming foundation

### Scope of this release

The original four projects have been **renamed consistently**, including directories, project files, solution entries, assembly/namespace references, Razor imports, project references, and active regression scripts:

| Before | After |
|---|---|
| `HealthMonitoringApp.Domain` | `NTSW_CENOPS.Domain` |
| `HealthMonitoringApp.Application` | `NTSW_CENOPS.Application` |
| `HealthMonitoringApp.Infrastructure` | `NTSW_CENOPS.Infrastructure` |
| `HealthMonitoringApp.WebUI` | `NTSW_CENOPS.WebUI` |

The WebUI **root namespace** remains equivalent to `NTSW_CENOPS` (so Razor components are named `NTSW_CENOPS.Components.*`), while its assembly is `NTSW_CENOPS.WebUI`. Existing database names, connection strings, application URLs, identities, EF migrations, and SQL schemas have not been intentionally renamed. Historical `/docs` files remain historical records; their old paths are not migration instructions.

A **fifth, independent class library** was added:

`src/NTSW_CENOPS.FileStreaming/NTSW_CENOPS.FileStreaming.csproj` (net8.0).

There is deliberately **no reference from WebUI/Application/Infrastructure to FileStreaming yet**, no routes, no UI, no upload button, and no change to `Program.cs` or runtime behavior. The library is ready for review before integration.

### What FileStreaming implements now

- `IFileStreamingService` / `FileStreamingService`: raw streamed uploads, metadata lookup, open for reading, delete.
- `IFileStorage` / `LocalFileStorage`: private local filesystem, chunked streaming with a server-enforced maximum size, temporary writes and atomic publish after successful completion.
- `IFileMetadataStore` / `JsonFileMetadataStore`: durable JSON sidecar metadata. Intentionally an interchangeable first-stage implementation, not the final SQL metadata schema.
- `FileStreamingOptions`: explicit absolute storage root and configurable per-upload byte limit (default **25 MiB**).
- `StoredFile`, `FileUploadRequest`, `FileReadResult`: transport-agnostic contracts, **no `IFormFile` or Blazor types**.
- `FileSizeLimitExceededException`: typed failure for a limit breach.
- `AddCenopsFileStreaming(...)`: optional DI registration for future host integration. **Not invoked by CENOPS yet.**

Files are written under `<RootDirectory>/objects/<two-character-ID-prefix>/<opaque-guid>` and metadata under `<RootDirectory>/metadata/<prefix>/<opaque-guid>.json`. The user's original filename is *display metadata only*, not a physical path. A failure or cancellation while streaming leaves no published partial object.

**Important caveat:** byte writes and metadata writes are separate filesystem operations, not a database transaction. The service cleans up the object if metadata creation fails, but a process/power crash between the writes can leave an orphan. Reconciliation, retention, multi-process coordination, and proper database transactions are later-stage concerns.

### Why streams rather than IFormFile?

`IFormFile` is ASP.NET Core's HTTP multipart abstraction. It is useful **at the API boundary**, but it should not be the contract of a storage library. A `Stream` represents a sequence of bytes that may come from an HTTP request, a filesystem, a test, or a background processor.

The first iteration uses **bounded incremental reads**, so it does not allocate a huge `byte[]` for every upload. The caller retains responsibility for disposing the input stream. When reading files, the consumer must dispose the returned `FileReadResult` / `Content` stream.

### Example (future integration, not yet active)

```csharp
// Future host configuration, with a path OUTSIDE wwwroot, bin, and the deployment folder:
services.AddCenopsFileStreaming(options =>
{
    options.RootDirectory = @"D:\CENOPS-Storage";
    options.MaximumUploadBytes = 25L * 1024 * 1024;
});

// Future controller/service invocation:
// await using var source = ...; // Open an upload stream.
var file = await fileStreamingService.UploadAsync(
    new FileUploadRequest(source, "example.pdf", "application/pdf"), cancellationToken);

// Only from an already-authorized application service in a future release:
await using var data = await fileStreamingService.OpenReadAsync(file.Id, cancellationToken);
await data.Content.CopyToAsync(destination, cancellationToken);
```

**Deployment:** the configured storage root must be a durable server-side volume outside the static-file root and must be readable/writable only by the CENOPS service account. Back up this directory together with its future database references. Do not map the storage directory using `UseStaticFiles`.

### Deliberately NOT implemented yet

- No upload endpoints or user interface.
- No media-type verification (the client-provided content type is untrusted), image compression or previews.
- No antivirus/anti-malware scanning, advanced content validation or quarantine release flow.
- No authorization/ownership or per-Chat/Wiki/Event visibility (this is an **internal service**, not safe to expose directly over HTTP).
- No SQL Server metadata integration or attachment relations.
- No signed URLs, network service, S3/MinIO backend, or cross-node storage.

**Do not expose `OpenReadAsync`/`DeleteAsync` through public endpoints before the application authorization phase.** The library's APIs are internal building blocks, not a security boundary.

### Local validation

```bash
dotnet build NTSW_CENOPS.sln
dotnet run --project tests/FileStreamingRegression/FileStreamingRegression.csproj
```

The regression console exercises upload, metadata persistence across instances, read, size limits, deletion, and cleanup of failed writes.

### Next review topics

1. Confirm `FileStreaming` naming and public interface shape.
2. Decide whether we will use JSON sidecar metadata only for early learning or move straight to a SQL-backed `IFileMetadataStore`.
3. Implement a secure HTTP upload boundary and reusable `AppFileUpload` component **after** this isolated module is approved.
4. Add media validation, scanning, processing, and application-level attachment permissions in the planned stages.
