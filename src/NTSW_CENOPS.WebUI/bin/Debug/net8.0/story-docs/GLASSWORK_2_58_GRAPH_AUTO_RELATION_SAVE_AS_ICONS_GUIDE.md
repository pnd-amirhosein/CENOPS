# GLASSWORK 2.58 — Graph auto-connections, shared Save As icons, zero badge

**Base archive:** `NTSW_CENOPS_Glasswork_2_57.zip`  
**Screens:** `/tools/database-inspector` and `/tools/clean-code`  
**Product naming:** CENOPS / سیناپس (not “Synapse”).

## Changes

1. **No empty-selection warning in Query Export.** The parent item `خروجی کوئری` is simply disabled when the diagram selection is empty. It always shows the existing selection-count badge, including `0`; no `انتخاب جدول الزامی است` hint is rendered. The default reusable graph toolbar item follows the same convention. Other query export behavior is unchanged.
2. **Save As icons match the knowledge sidebar.** The Note, SQL and cURL SVGs are now stored centrally in `Components/SideBar/KnowledgeMenuIcons.cs`. Both the real sidebar entries and the Clean Code `ذخیره به‌عنوان…` modal use those *same* trusted application SVG constants. No unrelated icon package or duplicated hand-drawn icons were introduced. The modal has small scoped CSS rules for SVG sizing.
3. **Auto-connect newly inserted tables.** When a table enters the graph, the server obtains its FK metadata through the existing authorized `IDatabaseInspectorService`. If any FK joins that inserted table to a vertex **already present** on the field (or to another newly inserted vertex), it adds the actual FK edge, with its SQL Server ID, direction and composite-column label. This applies to both multiselect `جدول متمرکز` insertion and PK/FK relationship expansion. Graph layout and undo/redo are still owned by the existing shared renderer.

## Architectural notes

- `DatabaseInspectorPage.razor` now uses `LoadRelationsForNodesAsync()` and `ConnectInsertedNodes()` in both insertion paths. For up to six uncached additions, per-table metadata calls reuse the existing cache. For larger insertion batches, a single all-FK catalog query avoids per-table network roundtrips; only relevant table subsets are retained in the per-table cache.
- `ConnectInsertedNodes()` only creates edges **incident to a newly added vertex** and only if both FK endpoints are currently present. It does not resurrect links deliberately removed *between nodes that never left the graph*. Ordinary removals still prune dangling edges, and a reinserted table restores its valid FK relationships automatically.
- PK/FK menu paths prefetch metadata *before* mutating graph membership; a metadata failure leaves the visible graph unchanged. Table count limit and duplicate-edge avoidance continue to apply.
- The graph edge catalog remains metadata-only; **no SQL is executed against application tables**. Connections are rendered through the existing `DatabaseGraphEdge` and `RefreshGraph` pipeline.
- The numeric badge is a built-in `AppNestedMenuItem.Count`, not a second bespoke badge component.

## Modified files

| File | Purpose |
|---|---|
| `Components/Common/DatabaseGraphView.razor` | Remove empty-selection hint; preserve disabled state and `Count: 0`; update graph JS cache key |
| `Components/Common/GraphWorkspace.razor` | Make default Query Export disabled with zero badge |
| `Components/SideBar/KnowledgeMenuIcons.cs` | Central, trusted Note / SQL / cURL SVG definitions |
| `Components/Layout/NavMenu.razor` | Use shared icon definitions |
| `Components/Pages/CodeCleanupPage.razor` | Render same sidebar SVGs in Save As modal |
| `wwwroot/css/snippet-tools.css` | SVG size styling within modal choices |
| `Components/Pages/DatabaseInspectorPage.razor` | FK metadata cache, new-node edge reconciliation, insertion hooks |
| `wwwroot/js/database-inspector-graph.js` | Update exported artwork version |
| `Components/App.razor` | Update CSS cache keys |
| `tests/DatabaseInspectorGraphBrowser/test_graph_258.py` | Graph updates, shared icons, disabled badge, insertion-path assertions |
| `tests/DatabaseInspectorGraphBrowser/test_graph_255.py` | Expect current release label in exported image |

## Automated tests

```bash
python tests/DatabaseInspectorGraphBrowser/test_graph_258.py
python tests/DatabaseInspectorGraphBrowser/test_graph_251.py
python tests/DatabaseInspectorGraphBrowser/test_graph_252.py
python tests/DatabaseInspectorGraphBrowser/test_graph_253.py
python tests/DatabaseInspectorGraphBrowser/test_graph_254.py
python tests/DatabaseInspectorGraphBrowser/test_graph_255.py
python tests/DatabaseInspectorGraphBrowser/test_graph_257.py
python tests/DatabaseInspectorGraphBrowser/test_query_transfer_256.py
python tests/GraphInteractionBrowser/test_interactions.py
node --experimental-default-type=module --test tests/DatabaseInspectorGraphBrowser/test_query_256.mjs tests/DatabaseInspectorGraphBrowser/test_query_257.mjs
node --test tests/GraphTraversalRegression/test_routes.mjs
```

Tests cover the server insertion-path wiring statically, headless Chromium edge rendering as new nodes are supplied, Save As SVG validity, zero badge state, legacy graph interactions and 4,418 traversal cases. **A full .NET build / SQL Server integration run remains a local acceptance requirement**, since .NET SDK and SQL Server are unavailable in this environment.

## Manual acceptance

- [ ] With nothing selected, Share → Query Export shows a disabled action with `0`, no warning subtitle.
- [ ] Check the Note, SQL, cURL choices in Clean Code → Save As; the icons visually match the knowledge sidebar items.
- [ ] Select two tables in `جدول متمرکز` which have an FK: the line appears immediately without pressing PK/FK.
- [ ] Add a third table by PK/FK that relates to **two or more existing nodes**: all relevant FK lines appear.
- [ ] Remove a table and add it again: its valid edges return, while already-pruned edges between unrelated old nodes do not.
- [ ] Check composite FKs, cycles, multiple FKs between the same tables, and edge uniqueness.
- [ ] Undo/Redo, dropdown membership, PK/FK completion state, and query export still work.
