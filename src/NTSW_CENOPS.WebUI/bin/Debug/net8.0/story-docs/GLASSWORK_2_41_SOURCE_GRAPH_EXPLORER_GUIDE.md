# Glasswork 2.41 — Source Explorer workspace and hierarchical graph

Base: **Glasswork 2.40**. No database migration, Roslyn index schema change, or additional NuGet package required.

## Changes

1. **Workspace:** The Definitions + Symbol Details section has an independent collapse/expand control. Search retains its previous separate accordion. The existing exact-name resolver is untouched.
2. **Definition cards:** A clear symbol-kind badge, prominent symbol name and declaration line, subtle file name on the same line, and project name below. The selected state and keyboard button behavior remain intact.
3. **Symbol metadata:** Changed decorative metadata tiles to one compact property sheet, keeping signatures, namespace, project, containing symbol and file/line.
4. **References filter:** One input in the reference-header filters by project, relative file, containing declaration, kind, namespace and indexed source line. The **Tree** and **Graph** use the same filtered occurrences, with a result count.
5. **Reusable graph:** `SourceGraphView` is fed with `SourceGraphNode` / `SourceGraphEdge` data, while its interactive layout and controls live in `source-relation-graph.js`. Its hierarchy is **selected declaration → file → containing type/namespace context → member → exact indexed usage location**. Container groupings are presentation nodes, not independently validated type identifiers.
6. **Progressive expansion:** Hover a node for its plus/minus control; clicking expands or collapses the children **already resolved in the selected symbol's one-hop index**. Further references of another member/type will require separate Roslyn identity resolution in a future feature; this release does not claim to support recursive traversal.
7. **Graph interaction:** Drag nodes, pan the canvas, Ctrl+wheel to zoom, wheel to pan vertically, Shift+wheel to pan horizontally. The lower-left toolbar includes reset, zoom, fullscreen, highlight path-to-root, and focus/rearrange selected branch. Reset restores all positions and visibility.
8. **Graph appearance:** Complete names are shown on adaptive-width SVG cards (no forced ellipsis); colors distinguish definition, file, type, member and location. Selected nodes are repainted using cyan fill instead of a heavy outline. Hover details use a structured readable tooltip. The selected-node inspector has a centered contrasting close button and can show nearby indexed source code.
9. **Source viewer from fullscreen:** Opening a related source location exits graph fullscreen **before** invoking the existing page-level `AppModal`. This avoids the Fullscreen API hiding the modal outside the fullscreen element's subtree. The existing source-file authorization and maximum-size checks remain in force.

## Data/security boundaries

- The exact-name resolver's case-sensitive matching and paging behavior are unchanged.
- Reference grouping uses the existing `GetReferencesAsync` output. It does not parse source from an arbitrary client-provided path.
- Full file reading continues through the existing `GetSourceFileAsync`, validating permission, registered repository, indexed symbol, relative path and file limits.
- Code content is rendered with safe text nodes (`textContent`), not interpolated HTML.
- Graph display is limited to the first 280 **filtered** occurrence locations, with the complete set available in the Tree tab. Keep in mind that one occurrence may generate several graph grouping nodes.
- Stale or truncated source indexes continue to display their pre-existing warning. Reindex after modifying copied source to refresh locations.

## Modified files

- `src/HealthMonitoringApp.WebUI/Components/Pages/SourceExplorerPage.razor`
- `src/HealthMonitoringApp.WebUI/Components/Pages/SourceExplorerPage.razor.css`
- `src/HealthMonitoringApp.WebUI/Components/Common/SourceGraphView.razor`
- `src/HealthMonitoringApp.WebUI/Components/Common/SourceGraphView.razor.css`
- `src/HealthMonitoringApp.WebUI/wwwroot/js/source-relation-graph.js`

## Suggested local checks

1. Build the solution: `dotnet build` from solution root.
2. Search `BillOfLading` and confirm exact symbol declarations and original reference count.
3. Collapse and reopen the definition/details workspace while retaining the selected declaration.
4. Filter references using a project name, file name, or method/container fragment; switch between Tree and Graph, comparing the counts.
5. Expand nodes in the graph through all five levels; inspect a file/type/member/usage node and open related code.
6. In graph fullscreen, select a usage, open the source viewer, expand to the full source file, and verify that the modal is visible. Return to graph.
7. Try Ctrl+wheel zoom, regular wheel vertical pan, Shift+wheel horizontal pan, dragging, Reset, ancestor-path highlighting and branch focusing.
8. Try narrow viewport and large source trees, plus an account without `tools.source-intelligence` access.
9. Test solution restart and reloading a previously indexed repository.

## Verification completed here

- JavaScript syntax check through Node 22.
- CSS parsing through `tinycss2`.
- Chromium **in-memory** graph interaction tests covering node expansion, selection, reset, path fade, branch filtering, and all three wheel modifiers; no page-script errors.
- ZIP integrity and modified-source comparison.

**Not verified here:** an actual .NET/Razor compilation, SQL Server / authentication integration, and end-to-end behavior against your enterprise Roslyn index. Perform those local checks before deployment.
