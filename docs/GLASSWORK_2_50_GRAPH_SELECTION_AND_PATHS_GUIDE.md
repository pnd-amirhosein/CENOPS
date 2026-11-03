# Glasswork 2.50 — Shared graph selection, routes and isolation

## Baseline and scope

Based on NTSW_CENOPS Glasswork 2.49. This is a presentation/interaction update to the shared GraphWorkspace, SourceGraphView, and DatabaseGraphView. Database metadata services, application authorization, Source Intelligence index, SQL lab schema, and persistence remain unchanged.

## Interaction contract

| Gesture | Result |
| --- | --- |
| Click a vertex | Replace selection with exactly that vertex; show its inspection panel |
| Ctrl+click or Cmd+click a vertex | Toggle membership in the selected-vertex set, without clearing the other vertices |
| Click empty canvas (not a vertex/button) | Clear selection and close the inspection panel |
| Focus-neighbors toolbar button | Available with at least one selected vertex; fade all vertices/edges that are not the most recently selected vertex or one of its direct neighbors/incident links; **do not move the camera or change zoom** |
| Two-node paths toolbar button | Enabled **only when exactly two** vertices are selected. Highlight **every simple route** connecting those two vertices through currently displayed links. Fade all unrelated vertices and links; **do not move the camera or change zoom** |
| No route available | Keep view unchanged and show an error with AppNoticeService (bottom left, also visible fullscreen) |
| Shift+click either graph toolbar action | Isolate the highlighted subgraph (hide unrelated vertices/edges) and **refit the camera** to that subgraph |
| Reset | Restore all vertices, clear isolation and selection, reset layout/zoom |
| PK/FK buttons | Continue to expand real SQL Server catalog relationships without changing selection semantics |

`Ctrl+click` and `Cmd+click` use the pointer event's modifier state; this is additive, unlike a normal click.

**Path direction:** For exploration, a link can be traversed in either direction. SQL foreign keys retain their actual child/parent direction in metadata and their display position, but path highlighting answers the question “are these tables connected through displayed FK links?”, not “is a directed SQL join walk possible?”. No unknown or inferred FK links are created.

**What counts as every path?** All *simple* paths (no repeated vertex); allowing repeated vertices would create infinitely many walks around cycles. A linear-time biconnected-component/block-cut decomposition computes the **union of all participating edges and vertices**, rather than listing exponentially many paths. Multiple FK constraints between the same two tables remain distinct highlighted edges. The entire algorithm is shared between Source Explorer and Database Inspector in `wwwroot/js/graph-paths.js`.

Source Explorer operates on the currently revealed vertices, not collapsed branches. Database Inspector operates on loaded tables and FK edges, not connections it has not yet fetched. Disconnected paths are not fabricated.

## Architecture

- `GraphWorkspace.razor`: shift-key-aware toolbar events (`EventCallback<MouseEventArgs>`), separately enabled focus-neighbors and two-vertex path controls, with tooltip labels explaining Shift isolation.
- `wwwroot/js/graph-paths.js`: pure, reusable traversal routines `nearEdges` and `allRouteEdges`.
- `wwwroot/js/database-inspector-graph.js`: selection, edge-color palette, fade-only focus/path, isolate/refit, blank-canvas dismissal. Keeps sharp layout-based zoom and zoom/pan in normal modes.
- `wwwroot/js/source-relation-graph.js`: same selection and path behavior using SVG, preserving the source-specific hierarchy and expansion logic. Direct Roslyn expansion is **not** modified.
- `DatabaseGraphView.razor`, `SourceGraphView.razor`: JS-to-Blazor selection count updates, separate enable states, error toasts, Shift modifier forwarding, details dismissal, cache-version bump.
- CSS: selection and edge-highlight styling, fullscreen-safe notices for both graph types.

### Why isolate doesn't mutate backend data

Shift-isolation hides vertices in the renderer; it does not delete SQL FK relationships, reload or discard stored source information, or change the database. Reset restores the complete loaded model. Adding new relations after isolation restores the loaded graph so newly discovered tables do not become silently hidden.

## Regression tests

From the solution root:

```bash
# Pure graph theory; compares 4,418 random/simple node pairs with exhaustive DFS
node --experimental-default-type=module tests/GraphTraversalRegression/test_routes.mjs

# Headless Chromium: both renderers, additive selection, camera preservation,
# multiple routes, isolate/refit, graph color variations, blank-canvas dismissal
python tests/GraphInteractionBrowser/test_interactions.py

# Existing graph interaction/zoom/fullscreen regression
python tests/DatabaseInspectorGraphBrowser/test_graph.py
```

These browser tests require Python Playwright and an installed Chromium at `/usr/bin/chromium` (adjust locally if needed). The tests use synthetic graph data; they do **not** connect to your SQL Server.

## Manual acceptance checklist

1. In `ابزارها → بررسی دیتابیس`, pick a permitted external connection and select `core.Tenant` and a few more tables in the test database. Expand PK and FK until some cross-links and loops are visible.
2. Select one table; verify the path button is disabled while focus-neighbors is enabled. Ctrl+click a second table; verify the path button enables. Ctrl+click a third; verify it disables again.
3. Zoom in and pan, then click focus-neighbors. Check that camera coordinates and zoom are unchanged; only unrelated cards/lines fade.
4. Select exactly two connected tables, then click the route button. Confirm all valid alternative paths are highlighted. Try a disconnected pair and verify the fullscreen-safe error toast.
5. Hold Shift while clicking either action: only participating vertices/links remain and the camera refits; reset restores them.
6. Click the background, **not** a node: the upper-right SSMS-style inspection panel closes.
7. Repeat with `/tools/source-explorer` to check that selection and controls behave identically, without relying on successful semantic reindexing.

No migration, connection-string change or source reindex is required for 2.50.

## Verification status

- Automated tests above passed in this packaging environment.
- JavaScript syntax, CSS syntax, solution/project XML, and ZIP CRC integrity checked.
- **Not verified**: .NET/Razor compilation and live SQL Server queries; .NET SDK and SQL Server are not available here. Please run `dotnet build` and test the components on your development machine.
