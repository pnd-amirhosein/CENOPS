# Glasswork 2.54 — Database Inspector graph polish

Route: `/tools/database-inspector`  
Baseline: `NTSW_CENOPS_Glasswork_2_53.zip`  
Scope: shared graph workspace and SQL graph renderer; no database schema migrations.

## Patch notes

1. **Backspace removal:** Clicking a node, Ctrl-clicking multiple nodes, or Ctrl-dragging a selection rectangle selects the actual graph IDs. Pressing **Backspace** while the graph stage owns keyboard focus deletes all selected nodes and incident edges in one undoable topology change. Input fields, selects and contenteditable elements retain their normal Backspace behavior.
2. **Photoshop-like history:** Added an accessible, vertically scrollable history popover using the **ellipsis between Undo and Redo**. Each step has a Persian operation name. The current step is highlighted; future/undone entries are dimmed. Clicking any entry jumps to its full snapshot (graph topology, selection, positions, zoom/pan and highlights) without destroying future steps. An edit after jumping back discards the future branch. The existing history limit is 80 snapshots.
3. **Ctrl shortcuts:** Holding Ctrl/Meta reveals white `Ctrl+Z` and `Ctrl+Y` shortcut keycaps above toolbar Undo/Redo. Previous Shift keycaps remain intact. `Ctrl+Shift+Z` is also supported as Redo.
4. **Crisper zoom:** Panning uses device-pixel-aligned **layout offsets**, while zoom uses the browser's layout-level `zoom` so node text and SVG connections are repainted. Removed the GPU-composited transform/will-change combination that was raster-scaling the entire graph. This avoids the blurry scaling caused by compositing cached card textures at different zoom levels.
5. **Clear entire workspace:** New trash-outline action at the **far left** of the graph toolbar. It removes all graph nodes/edges through the same authoritative graph-replacement flow as ordinary removal; Blazor's focused-table dropdown is updated accordingly. The workspace remains mounted even when empty. **Undo restores the cleared graph**; Redo clears it again. The existing grid/layout Reset remains a separate view-only operation.
6. **Rounded node corners:** Introduced `.dbi-node-surface`, which clips the header and action footer to rounded interior corners while preserving overflow for external hover/remove badges.
7. **Direction badges:** The Shift/FK left-arrow badge is now anchored to the **top-left** of the minus button; Ctrl/PK right-arrow remains top-right. Minus SVG alignment is centered by explicit flex alignment and zero padding.
8. **One relationship layer only:** Ctrl + minus removes the node and its **direct PK children**. Shift + minus removes the node and its **direct FK parents**. Traversal is no longer recursive: grandchildren, grandparents, and other indirect nodes remain. Cycles and parallel foreign keys remain safe because affected IDs are a set.
9. **Marquee preview:** While dragging with Ctrl, every intersecting node gets a temporary teal outline, like desktop file selection. This is cosmetic until pointer-up; on release/cancel the previews are removed. Selected styling and Undo history change only when the gesture is committed.

## Architecture and implementation

| File | Responsibility |
|---|---|
| `src/HealthMonitoringApp.WebUI/wwwroot/js/graph-interaction-support.js` | Shared modifier keycaps; bounded, labeled graph timeline with `goTo(index)` |
| `src/HealthMonitoringApp.WebUI/wwwroot/js/database-inspector-graph.js` | One-hop topological removal, Backspace, marquee preview, crisp camera, clear action, history capture/replay |
| `src/HealthMonitoringApp.WebUI/Components/Common/GraphWorkspace.razor` | Reusable history popover and clear action, enabled only when parameters are supplied |
| `src/HealthMonitoringApp.WebUI/Components/Common/GraphWorkspace.razor.css` | Popover and keycap styling |
| `src/HealthMonitoringApp.WebUI/Components/Common/GraphHistoryItem.cs` | Shared typed history-item DTO |
| `src/HealthMonitoringApp.WebUI/Components/Common/DatabaseGraphView.razor` | Blazor JS interop for labeled history and clear action |
| `src/HealthMonitoringApp.WebUI/wwwroot/css/database-inspector.css` | Live-selection outlines, rounded card surface, centered/remove badges, crisp pan |
| `src/HealthMonitoringApp.WebUI/Components/Pages/DatabaseInspectorPage.razor` | Updated keyboard/removal guidance |
| `tests/DatabaseInspectorGraphBrowser/test_graph_254.py` | New Chromium regression coverage |
| `tests/DatabaseInspectorGraphBrowser/test_graph_252.py` | Camera test updated for layout-offset positioning |
| `tests/DatabaseInspectorGraphBrowser/test_graph_253.py` | One-layer removal expectations updated to the new product contract |

### State consistency

- Graph membership stays authoritative: node and relationship removal calls `ReplaceGraph`, where Blazor reconciles graph nodes/edges and the **جدول متمرکز** dropdown. No client-only hidden-node set is introduced.
- A history jump changing topology calls `RestoreGraph`; the incoming server update is recognized as a replay and does **not** create another history entry.
- History no longer resets simply because focused root IDs change. Switching databases still remounts the component with its `@key`, resulting in a fresh and correctly scoped history timeline.
- Undoing a clear restores nodes *and* relationships, positions, selection and camera state.

## Regression commands

From the project root, with Python Playwright + Chromium and Node.js installed:

```bash
python tests/GraphInteractionBrowser/test_interactions.py
node tests/GraphTraversalRegression/test_routes.mjs
python tests/DatabaseInspectorGraphBrowser/test_graph.py
python tests/DatabaseInspectorGraphBrowser/test_graph_251.py
python tests/DatabaseInspectorGraphBrowser/test_graph_252.py
python tests/DatabaseInspectorGraphBrowser/test_graph_253.py
python tests/DatabaseInspectorGraphBrowser/test_graph_254.py
```

All seven suites passed in the artifact environment, including 4,418 exhaustive route cases. The Node route suite printed only an ESM package warning. JavaScript syntax and ZIP integrity were checked. **The .NET SDK and SQL Server were not available here**, so compile and database-backed Blazor integration must be tested locally.

## Manual acceptance checklist

- Select two nodes with Ctrl+click, hit Backspace, inspect the table dropdown count, then Undo/Redo.
- Select a group with Ctrl+drag; see each candidate outlined *during* the drag; release and hit Backspace.
- Open the history ellipsis, click an early entry, then a later entry. Make an edit from an earlier entry and check that the redo branch is discarded.
- Clear all nodes with the leftmost toolbar action. Confirm the graph stays visible and Undo brings everything back, including dropdown selection.
- Test Ctrl + minus on `Parent → Child → Grandchild`: only Parent + Child disappear. Test Shift + minus on the grandchild: only Grandchild + Child disappear.
- Hold Ctrl and Shift separately to inspect the floating shortcut/direction badges.
- Zoom in/out repeatedly and compare the sharpness of table text, relation lines and rounded card corners.
- Check menu/history popover scrolling while graph is fullscreen or at a short viewport height.
