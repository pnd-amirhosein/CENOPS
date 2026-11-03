# Glasswork 2.53 — Database Inspector: desktop selection & topology editing

**Base:** `NTSW_CENOPS_Glasswork_2_52.zip`  
**Scope:** `/tools/database-inspector`, its existing reusable `DatabaseGraphView` renderer, the existing `AppDropdown`, and graph regression coverage. No database migration, database writes, or changes to Source Explorer's graph behavior.

## User-facing changes

1. **Ctrl + drag on empty graph space** draws a blue desktop selection marquee. Every graph card whose *rendered* bounds intersect the marquee joins the selection, so it works with CSS zoom, viewport panning, and repositioned cards. Ctrl+click individual cards and regular click/double-click keep their earlier behavior; ordinary drag still pans the canvas and dragging a node moves that node. Selection changes close the details inspector.
2. **Hover-only node remove** appears in the upper-right of each SQL card (minus icon, keyboard-accessible name, and tooltip). Clicking removes only the card plus its incident FK edges. It does not delete a SQL table.
3. **Ctrl + remove** recursively removes all currently loaded **PK descendants**: from a table, follow edges where `parentId` matches the current table and visit `childId`. A white badge with an outlined right arrow appears on the hovered minus icon while Ctrl is held.
4. **Shift + remove** recursively removes all currently loaded **FK ancestors**: from a table, follow edges where `childId` matches and visit `parentId`. A matching white badge with an outlined left arrow appears while Shift is held. Shift takes precedence if both modifiers are held. Both traversals use visited sets, so shared ancestors, self-relations, and cyclic schemas terminate safely.
5. **Two-way focused-table dropdown:** `جدول متمرکز` is now the set of **all actual diagram vertices**, not merely the initial root seeds. PK/FK expansions automatically select their new endpoints in the dropdown (and update its count). Deselecting a table in the dropdown removes that table and incident FK edges; checking a table not already on the graph adds it. Deleting, Shift-pruning, Undo, and Redo all update the dropdown, and each operation uses one shared graph-membership model. Known relationship endpoints are included as dropdown choices even if the base SQL table list omitted them.
6. **Shift + path / Shift + neighboring-relations tool** now **prunes the actual topology**, rather than just hiding elements. The retained node and FK edge IDs are sent to Blazor as a topology transaction, and all other cards and edges are removed. Future PK/FK expansion, view reset, and loaded-checkmark calculations therefore operate only on the real remaining graph. Undo can restore pruned data from the bounded client history without refetching SQL metadata.

## Internal architecture

- All user-initiated topology removals use the same `ReplaceGraph` JS-to-Blazor callback and validated `DatabaseGraphSnapshot` transaction in `DatabaseInspectorPage`. History replay uses the existing `RestoreGraph` callback and that same validation function. The diagram never alters database objects.
- `RefreshGraph` projects the authoritative vertex dictionary into both the renderer and `_focusedTableIds`. `SelectableTables` unites the database catalog with metadata-verified endpoints for dropdown options; no additional service calls are needed for these selections.
- The renderer also limits native-click suppression to the matching pointer event. A previous sticky suppression flag could swallow the next PK/FK or minus-button click after card redraw. The graph renderer **no longer stores an isolation/hidden-node mask**. Plain toolbar clicks still dim/highlight without deleting; Shift-click permanently reduces nodes and edges. `Reset` remains a geometry/camera cleanup only.
- Explicit `FocusedRootIds` forwarding was removed from the page. Previously changing that list reset the graph camera every time a node was added; the renderer now maintains its layout across topology changes. Component identity is still reset when the SQL source changes.
- The empty graph workspace stays mounted after removing the last node, preserving Ctrl+Z/Undo; it is destroyed when the database source is changed.
- Node removal is staged as a single graph replacement and recorded by the existing bounded undo/redo history when the Blazor update is applied. No new external JS dependencies or components were introduced.

## Changed files

- `src/HealthMonitoringApp.WebUI/Components/Pages/DatabaseInspectorPage.razor`
- `src/HealthMonitoringApp.WebUI/Components/Common/DatabaseGraphView.razor`
- `src/HealthMonitoringApp.WebUI/wwwroot/js/database-inspector-graph.js`
- `src/HealthMonitoringApp.WebUI/wwwroot/css/database-inspector.css`
- `src/HealthMonitoringApp.WebUI/Components/App.razor` (asset cache version)
- `tests/DatabaseInspectorGraphBrowser/test_graph_251.py` (destructive pruning behavior)
- `tests/DatabaseInspectorGraphBrowser/test_graph_253.py` (new browser coverage)
- `tests/DatabaseInspectorGraphBrowser/fixture.html` (asset cache version)
- `tests/GraphInteractionBrowser/test_interactions.py` (Shift-prune integration with topology callback)

## Verification commands

```sh
python tests/DatabaseInspectorGraphBrowser/test_graph.py
python tests/DatabaseInspectorGraphBrowser/test_graph_251.py
python tests/DatabaseInspectorGraphBrowser/test_graph_252.py
python tests/DatabaseInspectorGraphBrowser/test_graph_253.py
python tests/GraphInteractionBrowser/test_interactions.py
node --experimental-default-type=module tests/GraphTraversalRegression/test_routes.mjs
node --check src/HealthMonitoringApp.WebUI/wwwroot/js/database-inspector-graph.js
```

The new browser tests use Chromium and include additive Ctrl-marquee, CSS hover affordance, Ctrl PK descendant removal with a cycle, Shift FK ancestor removal with a cycle, single-node deletion, removing the last node and undoing it, undo, layout reset, neighbor pruning, and path pruning that discards out-of-path FK edges. Existing graph traversal tests exhaustively validate 4,418 route cases.

**Manual checklist (requires the running Blazor app):**

- Select a source and two unrelated tables from `جدول متمرکز`. Check Ctrl-drag across both; then drag a card and pan the background without Ctrl.
- Expand a PK and an FK, verify their new table names and live count in the dropdown. Uncheck an expanded table; verify its edges disappear and PK/FK completion is recalculated. Check it again to re-add it, then re-expand any missing relations.
- Use minus, Ctrl+minus, and Shift+minus on a schema with chains and cycles. Confirm the expected nodes and related edges disappear, the white directional badges are visible, and the dropdown count stays in sync.
- Select two connected tables and Shift-click the path action; verify unrelated branches and non-path edges are **absent**, not hidden. Expand other relations afterward; unrelated deleted nodes must not silently return. Repeat for Shift-click neighboring-relations action.
- Click Reset after pruning; it must not restore deleted topology. Undo/Redo must restore/remove topology and synchronize the dropdown. Remove the **last** table; Undo should still be possible from the empty graph.
- Confirm that removing a node never changes the actual SQL database. Check normal selection, double-click design inspection, PK/FK menus, the bottom-left Persian toast, and fullscreen rendering.

**Environment:** The .NET SDK and a live SQL Server are unavailable here. Run a full solution build and exercise database calls locally; the JS browser/regression suites and static integrity checks run in the packaging environment.
