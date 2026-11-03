# GLASSWORK 2.57 — Database Inspector export preview, path-based JOIN and Ctrl+A

**Baseline:** `NTSW_CENOPS_Glasswork_2_56.zip`  
**Page:** `/tools/database-inspector`  
**Brand:** CENOPS (سیناپس), not “Synapse”.

## Changes

1. **SQL dialog hosted at application level.** The database query `AppModal` no longer renders inside `DatabaseGraphView` / the clipped `dbi-graph` container. A named `cenops-global-overlays` `SectionOutlet` is registered in `MainLayout` outside the isolated `.app-shell`. The existing `AppModal` stays reusable. The backdrop now covers the entire Skyline, sidebar, database selector box, and diagram, without any selector popover appearing above the preview. The overlay host retains the Persian system typography. When opening a query preview from graph fullscreen, the graph exits fullscreen first: browser fullscreen top-layer elements otherwise obscure ordinary application modals.
2. **Ctrl+A selects all graph nodes** while keyboard focus belongs to the graph, with both Ctrl and macOS Cmd accepted. The selection, toolbar state, graph selection highlighting, current inspector, and history are synchronized through the same existing selection-state callback. The shortcut does not intercept typing or selecting text inside inputs, textareas, contenteditable elements, or selects.
3. **Numeric query-selection badge.** The reusable `AppNestedMenuItem` model supports an optional `Count`; the query parent action displays `_selectedIds.Count` in the existing export drawer, including zero when query export is disabled. The badge updates with click/multiselect/marquee, deletion, clearing, and undo/redo selection notifications. Other consumers remain unchanged.
4. **Full-schema relationship discovery.** The service adds `GetAllRelationsAsync`, which reads FK relationships and complete composite-key column mappings through one authorized, metadata-only `sys.*` query. The old per-table `GetRelationsAsync` remains unchanged for interactive graph expansion.
5. **Shortest-path JOIN routing through unselected tables.** `planJoinPaths(selectedIds, foreignKeys)` traverses the **full FK catalog**, not just selected/visible graph nodes. Relationships are undirected for navigation (the SQL ON predicates still respect child/parent column mappings). Deterministic BFS picks the smallest FK-edge-count path, including three or more intermediate JOIN tables; an alternative shorter path wins. With more than two selected tables, a multi-source BFS repeatedly connects the closest not-yet-connected selection to the existing join tree without duplicating tables. This is a practical Steiner-tree heuristic for multiple terminals, **not a guarantee of globally minimal total edges for 3+ terminals**. FK-disconnected selected groups remain separate SELECT statements. Cycles and parallel FKs are safely handled.
6. **SQL projection rules.** JOIN bridge tables are included in FROM/JOIN/ON but only the **originally selected** tables contribute output columns; their columns are explicitly listed and given bracket-quoted aliases (never `SELECT *`). The plan includes FK IDs and table IDs only; the Blazor server subsequently authorizes/loads designs for all needed bridge tables. The existing SQL copy and Clean Code transfer paths are unchanged, and SQL is never executed.
7. **GLASSWORK 2.57 image watermark**, CSS/JS cache-key updates, and release-specific tests.

## Code changes

| File | Change |
|---|---|
| `Components/Layout/MainLayout.razor` | Global `SectionOutlet` and wrapper above the app shell |
| `wwwroot/css/operations-shell.css` | Overlay host stacking, typography |
| `Components/Pages/DatabaseInspectorPage.razor` | Move SQL preview out of graph; load catalog, choose bridge designs, pass original selection |
| `Components/Common/DatabaseGraphView.razor` | Numeric badge; exit fullscreen before modal; JS cache version |
| `Components/Common/DatabaseGraphModels.cs` | `DatabaseJoinPlan` metadata DTO |
| `Components/Common/Menus/AppNestedMenuItem.cs` | Optional `Count` field |
| `Components/Common/Menus/AppNestedMenu.razor` / `.razor.css` | Reusable numeric badge |
| `wwwroot/js/database-inspector-graph.js` | Ctrl+A; fullscreen dialog handoff; 2.57 watermark |
| `wwwroot/js/database-query-export.js` | Reusable shortest-hop planner and selected-only projection for JOIN |
| `Application/DatabaseInspector/DatabaseInspectorContracts.cs` | Full FK catalog method |
| `Infrastructure/DatabaseInspector/SqlServerDatabaseInspectorService.cs` | One metadata-only FK catalog read with reused mapping parser |
| `Components/App.razor` | Updated CSS cache keys |
| `tests/DatabaseInspectorGraphBrowser/test_query_257.mjs` | SQL routing coverage |
| `tests/DatabaseInspectorGraphBrowser/test_graph_257.py` | Ctrl+A and layering smoke tests |

## Verification commands

```bash
node --experimental-default-type=module --test tests/DatabaseInspectorGraphBrowser/test_query_256.mjs tests/DatabaseInspectorGraphBrowser/test_query_257.mjs
node --test tests/GraphTraversalRegression/test_routes.mjs
python tests/DatabaseInspectorGraphBrowser/test_graph_257.py
python tests/DatabaseInspectorGraphBrowser/test_graph_255.py
python tests/DatabaseInspectorGraphBrowser/test_query_transfer_256.py
python tests/DatabaseInspectorGraphBrowser/test_graph_254.py
python tests/GraphInteractionBrowser/test_interactions.py
```

Other existing `test_graph*.py` browser suites were rerun too. Tests cover multi-hop and alternate shortest routes, split disconnected groups, composite FK predicates, SQL aliases, cycles, editor-safe Ctrl+A, and click/context menu compatibility.

## Manual acceptance on live application

- [ ] With the SQL preview open, the Skyline, sidebar, and both database dropdowns are **behind** the modal backdrop.
- [ ] Open Query Export from graph fullscreen: fullscreen exits before the preview appears; no browser top-layer occlusion.
- [ ] Ctrl+A in graph selects every displayed node and updates the query count badge; Ctrl+A in an input edits only that input.
- [ ] Ctrl-click and Ctrl-drag selections update the menu badge after every edit, deletion and Undo/Redo.
- [ ] Select tables connected through three or more FK hops **without adding bridge tables to the graph**; SQL includes their JOINs.
- [ ] With alternative paths, confirm that the fewer-hop route is chosen, with full FK `ON` mappings.
- [ ] Select tables in independent FK components; each group produces its own query and isolated tables produce a simple SELECT.
- [ ] Copy and Clean Code transfer still populate the expected SQL.
- [ ] Image export still opens in a new tab and shows the 2.57 branding.

**Environment limit:** This archive's tests use Node and headless Chromium. The .NET SDK and a live SQL Server were unavailable here; build and SQL integration must be verified in the target environment.
