# Glasswork 2.38 — Source Explorer Layout, Reference Tree & Full-File Viewer

Baseline: **Glasswork 2.37**. This release changes only Source Intelligence exploration/presentation and its authorized source-file reading contract. It leaves exact, case-sensitive declaration matching and indexing unchanged.

## Summary

1. **Developer terminology**: `Namespace` is displayed in English in metadata and kind labels. No translated `فضای نام` labels on the Source Explorer page.
2. **Search accordion**: Search input, repository scope, and declaration-kind chips can be collapsed from a single header. When collapsed, the resolved symbol name remains visible.
3. **Separated layout**: Search occupies its own collapsible panel. Below it, two separate cards show **definition results (right)** and **selected symbol information (left)**. Pagination belongs to the results card.
4. **Full-width references**: An independent section underneath both columns presents a project → file → usage tree. Project/file levels use native, keyboard-accessible `<details>/<summary>` accordions and visible plus/minus controls; usage leaves display line/column, containing member, source line, and implementation flag when applicable.
5. **Expanded code preview**: Clicking a usage opens the existing short context modal. `نمایش کل فایل` loads the entire current `.cs` file, widens the modal, and displays a read-only file viewer with line numbers, highlighted occurrence, virtualized rows, and `Jump to L…`. The user can return to short context or close via the existing modal action/Escape.

## Architecture

### UI

- `SourceExplorerPage.razor` / `.razor.css`: responsive three-part layout, search collapse state, native tree accordions, code modal toggle. Selecting a **usage** never changes the **selected declaration**.
- `SourceFileViewer.razor` / `.razor.css`: reusable read-only full-file component, using Blazor `Virtualize` with 26px fixed-height lines. Individual lines are safely escaped by Razor (no raw HTML or source-code execution). Horizontal scrolling is supported for long code lines. Persian text within source lines uses bidi-safe plain-text behavior.
- `wwwroot/js/source-file-viewer.js`: minimal scroll positioning for the referenced line. No extra UI framework/dependency.

### Service/API

- `SourceFileResult(File, Content, LineCount, MayDifferFromIndex)` is a new application contract.
- `ISourceIntelligenceService.GetSourceFileAsync(actor, repositoryId, symbolId, relativeFile, cancellationToken)` reads a file **on demand**.
- `SourceIntelligenceService` authorizes with the existing Source Intelligence permission, verifies the repository registration/index and symbol identity, and verifies that the requested file is referenced by that selected symbol or is its declaration/implementation.
- The file path is constructed from the configured registered source root. Parent traversal, non-`.cs` files, linked files/directories, paths outside the registered directory, and files over the configured `MaxFileBytes` threshold are rejected. No files are exposed through `wwwroot` or direct URLs.
- The full-file viewer reads **current filesystem content**, not a captured full-file snapshot. It shows a best-effort warning if the file's modification time is newer than the index timestamp. Some filesystems preserve timestamps; for a guaranteed historical version, a snapshot store would be needed in a later release.

## Changed files

| File | Reason |
|---|---|
| `src/HealthMonitoringApp.WebUI/Components/Pages/SourceExplorerPage.razor` | Three-section structure, accordion search/tree, full file modal |
| `src/HealthMonitoringApp.WebUI/Components/Pages/SourceExplorerPage.razor.css` | Responsive layout, tree affordances and modal toolbar |
| `src/HealthMonitoringApp.WebUI/Components/Common/SourceFileViewer.razor` | New reusable line-numbered virtualized viewer |
| `src/HealthMonitoringApp.WebUI/Components/Common/SourceFileViewer.razor.css` | File-viewer styling |
| `src/HealthMonitoringApp.WebUI/wwwroot/js/source-file-viewer.js` | Jump-to-reference behavior |
| `src/HealthMonitoringApp.Application/SourceIntelligence/SourceModels.cs` | Secure file-read contract/result |
| `src/HealthMonitoringApp.Infrastructure/SourceIntelligence/SourceIntelligenceService.cs` | Authorized, size-bounded, path-bounded source-file reading |

## Deployment

- Replace the changed files or the full project and build as usual.
- **No EF migration** and **no configuration changes**.
- **No reindex required** when your existing index has the direct reference data from 2.37. Reindex only if your current reference result is incomplete/stale.
- Keep the existing `SourceIntelligence:RootPath`, `IndexPath`, and `MaxFileBytes` configuration. The worker's read identity must have read access to registered source folders.

## Local testing checklist

1. Search exact `BillOfLading`; confirm matching definitions/counts unchanged from 2.37.
2. Collapse/reopen search: result list and selected declaration remain intact.
3. Select a declaration. Check layout: results right, symbol metadata left, references full width beneath them.
4. Expand/collapse project and file levels with mouse **and keyboard** (Enter/Space on summary); plus/minus state follows expansion.
5. Click usage in one file and another file; check the selected symbol remains pinned.
6. Open short snippet then choose `نمایش کل فایل`; verify modal enlargement, correct file content, numbered lines, target-line highlight and jump, horizontal scrolling, and scrolling performance in a long source file.
7. Return to context, close with Escape/backdrop; reopen another usage and check no previous file is carried over.
8. Modify a source file after indexing and reopen; verify the changed-file warning when filesystem timestamps permit detection. Reindex after the modification.
9. Verify users without Source Intelligence permission cannot obtain full-file content; test a made-up relative path, `../` path, arbitrary unrelated file, linked path, and a file larger than `MaxFileBytes` via service tests.
10. Test desktop/tablet/mobile widths and Windows paths containing spaces and Persian text in strings/comments.

## Validation performed in this delivery environment

- ZIP integrity and file comparison.
- Modified JavaScript syntax and jump-to-line tests.
- CSS parsing and project XML validation.
- Targeted source checks for UI structure, service authorization, and path checks.

**Not verified here:** full .NET/Razor build, Blazor integration, live index/repository I/O, and accessibility/browser behavior with a real application. The environment does not have the .NET SDK. Review on your Windows deployment after compiling.
