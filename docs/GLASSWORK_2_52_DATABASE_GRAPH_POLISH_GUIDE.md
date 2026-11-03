# Glasswork 2.52 — Database Inspector graph UI and relationship completeness

**Base:** `NTSW_CENOPS_Glasswork_2_51.zip`. No SQL migration, table schema, authentication or service API changes. The graph's HTML/CSS/JS and existing Blazor metadata model are reused. `GraphWorkspace` remains shared with Source Explorer.

## Fixes

1. **PK/FK popup positioning:** `positionMenu` now measures the actual rendered picker, button and available graph boundaries. It opens below the PK/FK button when space permits, flips above near the graph bottom, and clamps to the stage on either axis. The popup has bounded height with scrolling, preserving access to the checkbox list and action buttons even on short screens. The same positioning routine serves both PK and FK.
2. **Distinct history / layout icons:** Reset now uses a four-tile layout icon, while Undo and Redo use clearly directional return arrows. Existing tooltip labels, callbacks, history scope and shortcuts are retained.
3. **Shift hints:** Removed the text-glyph pseudo-element. Both path/focus actions now render the same accessible-hidden, white keycap with an outlined Shift SVG (2.5px stroke), subtle border and shadow. Indicators track physical key-down/keyup/window blur via the existing shared helper; they stay legible even on a disabled action. This change also applies to Source Explorer via `GraphWorkspace`.
4. **Clear visual selection:** Choosing a focused table from the dropdown no longer assigns a permanently highlighted `.focus` card style. Only temporary `.selected` cards receive the active border. Clicking empty graph space clears the visual selection and closes the details panel. The dropdown's selected root list and graph topology are unaffected.
5. **Accurate PK/FK completion:** Button checkmarks and green loaded styling are computed from the **entire directional SQL relationship set**: all relationship endpoints must be present *and* all matching edges must have been added to the graph. A partial expansion, an omitted node at the 400-table limit, an undo, or a removed related table cannot leave a false complete marker. Empty relation sets never show a misleading checkmark. Previously loaded edges are excluded from the picker when reopening a partial expansion.
6. **Completed-branch toast:** Clicking a fully expanded PK or FK displays a Persian `AppNoticeService.Info` notice (the existing bottom-left system toast). It does not open a redundant picker or issue another SQL metadata request. A completed-click also closes any previously open relation picker. Unrelated/empty branches keep the previous no-relations notices.

## Implementation notes

- `DatabaseInspectorPage.razor` maintains a per-source, per-table in-memory relation metadata catalog. It is discarded when the source graph is reset, so completion is derived from current vertices/edges and known SQL constraints rather than a latched "loaded" flag. It is intentionally **not** a database write or durable cache. To refresh externally changed schema metadata, switch/reselect the database source.
- `DatabaseGraphView.razor` imports the renderer with `?v=2.52`; `App.razor` bumps CSS cache versions, including the scoped shared component stylesheet.
- The picker still follows the existing single-relationship quick-add behavior. Where multiple relationships exist, the picker lists only not-yet-added connections, even if one remains.
- Existing graph history restores nodes/edges, with loaded styles naturally recalculated from the catalog after Blazor state restoration. No new graph component or duplicate toast mechanism was introduced.

## Changed files

- `src/HealthMonitoringApp.WebUI/Components/Pages/DatabaseInspectorPage.razor`
- `src/HealthMonitoringApp.WebUI/Components/Common/DatabaseGraphView.razor`
- `src/HealthMonitoringApp.WebUI/Components/Common/GraphWorkspace.razor`
- `src/HealthMonitoringApp.WebUI/Components/Common/GraphWorkspace.razor.css`
- `src/HealthMonitoringApp.WebUI/wwwroot/js/database-inspector-graph.js`
- `src/HealthMonitoringApp.WebUI/wwwroot/css/database-inspector.css`
- `src/HealthMonitoringApp.WebUI/Components/App.razor`
- `tests/DatabaseInspectorGraphBrowser/test_graph.py` (old permanent-focus assumption removed)
- `tests/DatabaseInspectorGraphBrowser/test_graph_251.py` (updated Shift badge fixture)
- `tests/DatabaseInspectorGraphBrowser/test_graph_252.py` (new browser test)
- `tests/DatabaseInspectorGraphBrowser/fixture.html` (module cache version)

## Checks executed

```sh
python tests/DatabaseInspectorGraphBrowser/test_graph.py
python tests/DatabaseInspectorGraphBrowser/test_graph_251.py
python tests/DatabaseInspectorGraphBrowser/test_graph_252.py
python tests/GraphInteractionBrowser/test_interactions.py
node --experimental-default-type=module tests/GraphTraversalRegression/test_routes.mjs
node --check src/HealthMonitoringApp.WebUI/wwwroot/js/database-inspector-graph.js
```

All five browser/graph regression suites pass. Traversal suite exhaustively compares 4,418 route sets. The new Chromium suite verifies bottom-anchored PK/FK flipping, stage edge clamping, short-stage scroll behavior, white outlined Shift hints and blank-space deselection.

**Manual acceptance on a running app:** Select a database, add a root table, expand several of its PK/FK links in two passes, verify the checkmark appears only after the last connection is added; click completed PK/FK to see the Persian toast at bottom-left (also check fullscreen). Test a menu near the bottom of the graph and while zoomed. Verify empty-space deselection, then Undo/Redo after a partial expansion. Test Shift hints on enabled and disabled actions.

**Environment limitation:** The .NET SDK and SQL Server aren't installed in the packaging environment, so a full Razor build and live metadata integration checks still need to run on the developer machine.
