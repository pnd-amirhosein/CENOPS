# Glasswork 2.42 — Progressive Source Graph

Base: **Glasswork 2.41**. Focus: progressive symbol-reference traversal, fullscreen source inspection, and graph usability.

## What changed

1. **Compact graph canvas**: default graph height is 490px on desktop / 430px on narrow screens; fullscreen remains viewport-sized.
2. **Fullscreen source viewer**: the existing code preview/full-file modal is now rendered *inside* `SourceGraphView`'s fullscreen DOM subtree through a `ModalContent` render fragment. Opening source code no longer calls `document.exitFullscreen()`. The same `SourceUsagePreviewDialog` is reused in Tree and Graph views.
3. **Long node labels**: the graph calculates wrapped text lines and actual node height rather than truncating labels. Node geometry and edge positions use these heights. The hover tooltip intentionally truncates long metadata with ellipsis.
4. **Progressive exploration**: a type or member node with a **resolved declaration identity** has an on-hover `+` action. Clicking it requests *that symbol's* direct references, groups their occurrences under that node, and leaves all other branches alone. Each subsequent resolvable member/class can be expanded independently.
5. **Index identity**: new indexes include `MemberSymbolId` and `TypeSymbolId` alongside each occurrence, obtained from Roslyn's actual semantic symbols. Version-3 indexes remain readable; the service has a best-effort relation-based fallback for their context.
6. **Safe full-file access for deeper hops**: the file-preview request uses the actual expanded usage's `SymbolId`, so the existing permission-checked indexed-file API can authorize that location rather than incorrectly requiring it to be a reference to the original search symbol.
7. **Viewport continuity**: graph updates retain pan/zoom/selected node instead of remounting a fresh graph on each expansion.

## Expansion semantics

The graph initially shows direct usages of the exact searched declaration. To explore further:

1. Expand a file/type/member context in the initial graph.
2. Hover over a class or method/property whose Roslyn identity is resolved.
3. Click its `+`. CENOPS calls `GetReferencesAsync` with **that declaration ID**, not the root symbol ID.
4. The results are grouped as `clicked symbol → file → type → member → usage` directly below the clicked node.
5. Repeat on a new class/member to take another hop.

This is **reference exploration**, not guaranteed runtime call tracing: DI injection, reflection, conventions, dynamic dispatch, missing external references, and generated code can hide runtime relationships. Parent grouping nodes describe *source context*. They are not fabricated edges between methods.

Each branch can expand independently. Cycle protection stops a symbol identity repeating on the *same ancestor path*. The graph also limits its first-hop occurrences to 280, each additional expansion to 100, visible model to 1500 nodes, and nested materialization to 32 levels. These bounds protect the interactive Blazor circuit; the original reference tree remains available for the first-hop results.

## Source index upgrade

The persisted JSON index schema is now version **4**. It adds two optional IDs to `SourceUsage`; prior data is backward compatible.

**After deploying, reindex your registered sources once.** This is recommended for accurate multi-hop expansion, especially declarations in nested classes, properties, and method bodies. Old indexes still load through a best-effort fallback, but some nodes may not offer expansion when ownership cannot be resolved.

No database schema migration, new NuGet package, or `appsettings` change is necessary.

## Changed files

- `src/HealthMonitoringApp.Application/SourceIntelligence/SourceModels.cs` — optional enclosing-symbol IDs.
- `src/HealthMonitoringApp.Infrastructure/SourceIntelligence/SourceIntelligenceService.cs` — semantic ownership at index time, version-4 compatibility, version-3 fallback.
- `src/HealthMonitoringApp.WebUI/Components/Common/SourceGraphModels.cs` — optional expansion target ID.
- `src/HealthMonitoringApp.WebUI/Components/Common/SourceGraphView.razor` — on-demand graph expansion, modal render slot, viewport-preserving updates.
- `src/HealthMonitoringApp.WebUI/Components/Common/SourceGraphView.razor.css` — compact canvas, fullscreen modal stacking, better hover ellipsis.
- `src/HealthMonitoringApp.WebUI/Components/Common/SourceUsagePreviewDialog.razor` — reusable short/full-file modal.
- `src/HealthMonitoringApp.WebUI/Components/Common/SourceUsagePreviewDialog.razor.css` — modal toolbar styling.
- `src/HealthMonitoringApp.WebUI/Components/Pages/SourceExplorerPage.razor` — bounded branch building, cycle detection, and per-hop source locations.
- `src/HealthMonitoringApp.WebUI/wwwroot/js/source-relation-graph.js` — progressive expansion, dynamic label wrapping and layout, state-preserving updates.

## Security and data consistency

The page cannot supply arbitrary symbol IDs to the filesystem reader. Each expansion is checked against a graph node that was produced by the server and is routed through `ISourceIntelligenceService.GetReferencesAsync`, which enforces `tools.source-intelligence`. The full-file reader still verifies registration, indexed symbol and file location, path containment, and file-size limits.

Graph state is per interactive page instance; no new records or source files are created by navigation. When filtering usages or selecting a new declaration, stale expansion paths are discarded.

## Local validation checklist

1. `dotnet restore && dotnet build` in the solution root.
2. Reindex one representative multi-project solution via **مدیریت منابع و ایندکس**.
3. Search an exact declaration such as `BillOfLading` and inspect its graph.
4. Expand the actual class or `DbSet` property's node. Check that references of **that symbol**, not those of the original search symbol, are added.
5. Expand a newly discovered method node and compare its results against Visual Studio **Find All References**.
6. Follow a cyclic reference chain; check that the graph reports cycle protection instead of looping.
7. Open a deep-hop source snippet, select **نمایش کل فایل**, and confirm the authorized file loads.
8. Enter Graph fullscreen, open the code modal, expand the file, close it and verify Graph is **still fullscreen**.
9. Check wrapped filenames, tooltips, mouse pan/zoom, draggable nodes, reset, path-highlight and branch focus.
10. Test a large repository with unresolved NuGet dependencies; note which relations cannot be proven by Roslyn.

## Verification performed in this environment

- JavaScript parse check (`node --check`).
- Browser interaction fixture: two progressive expansion callbacks, wrapped label geometry, fullscreen persistence with a descendant modal, and absence of runtime script errors.
- Project XML and ZIP integrity / changed-file comparisons.

**Not verified:** .NET/Razor compilation, real EF/database access, and end-to-end semantic indexing in the deployed Blazor app. The actual Roslyn results must be validated in your environment.
