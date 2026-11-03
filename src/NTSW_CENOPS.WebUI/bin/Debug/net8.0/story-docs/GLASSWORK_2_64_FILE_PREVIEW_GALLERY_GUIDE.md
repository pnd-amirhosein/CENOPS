# C.E.N.O.P.S. GLASSWORK 2.64 — File preview and gallery polish

## Scope

This release extends the reusable Note file block without introducing scanning, compression, sharing policies, or new Note database tables.

- Extracted **`Components/Files/AppFilePreview.razor`**: one read-only viewer for gallery and full-size `AppModal`, independent from Notes.
- Added **`IFilePreviewReader` / `FilePreviewReader`** to `NTSW_CENOPS.FileStreaming`, which extracts a *bounded, read-only representation* from raw stored files. The single boundary remains `IAppFileGateway` in Application, implemented by `AppFileGateway` in Infrastructure.
- **PDF**: authenticated `/api/files/{id}/preview` endpoint; verifies `%PDF-` magic prefix and serves the original as `application/pdf` for the browser's built-in viewer. Other active uploads are never exposed via an inline HTML/SVG endpoint.
- **Office**: DOCX text and XLSX tabular preview via the .NET built-in `ZipArchive`/`XDocument` implementation. PPTX text extraction is also included. No LibreOffice process, online conversion, or external service is required. Layout/formatting fidelity is intentionally limited.
- **Text/code**: `txt`, `md`, `cs`, `css`, `scss`, `html`, `htm`, `cshtml`, `razor`, `js`, `jsx`, `ts`, `tsx`, `xml`, `svg`, `json`, `sql`, `yaml`, `log`, etc. Rendered as encoded text, **never executed**. CSV/TSV are parsed into bounded spreadsheet previews.
- Unsupported binary formats (e.g. legacy `.doc`, `.xls`, PPT binary) show a clear download-only fallback; this is not a binary-Office converter.
- Bounded extraction: 2 MiB for source/text, 20 MiB for modern Office archives, 3 MiB per XML entry, 150 spreadsheet rows × 32 columns × five sheets. Previews never modify stored files.

## UI

- Gallery's outer outline removed; full-width/full-height preview canvas, fitting images without cropping. Source/PDF/table previews use the same space. Opening the modal is an explicit overlay control.
- Compact pending-file rows: stronger filename weight, hover/touch-accessible remove button, count badge on the upload action. Existing upload prechecks and delete confirmation stay unchanged.
- Updated `FileUploadPolicy` accepted extensions to match the new text/code preview formats.

## Storage location

Unless overridden by `FileStreaming:RootDirectory`, DI defaults to `Environment.SpecialFolder.LocalApplicationData` + `NTSW_CENOPS/file-storage` on the application server. The library stores bytes under `objects/{first-two-id-chars}/{guid}` and JSON metadata under `metadata/{first-two-id-chars}/{guid}.json`. The upload folder is not served through static files.

## Verification

- `python tests/FilePreviewBrowser/test_preview_264.py`: browser presentation and static architecture checks.
- `python tests/FileBlockBrowser/test_file_block.py`: original drop/picker and compact queue regression.
- `dotnet run --project tests/FileStreamingRegression/FileStreamingRegression.csproj`: C# upload+preview regression (requires .NET 8 SDK).
- `dotnet build NTSW_CENOPS.sln`: verify the application before deployment.

**Current roadmap limitations:** no malware scan, per-Note access policy, compression, converter for legacy Office files, or orphan file cleanup. The browser's PDF viewer availability varies by browser and browser policy. A full .NET compile/integration test was unavailable in the authoring environment.
