# Glasswork 2.51 — Database Inspector graph refinement and history

## Base and boundaries

Built on the supplied **NTSW_CENOPS_Glasswork_2_50.zip**. The SQL Server metadata queries, connection permissions, authentication, EF migrations and Deep GraphLab fixture are unchanged. `GraphWorkspace` is still the shared toolbar for Source Explorer and Database Inspector; the graph journal is enabled only on Database Inspector.

## Changes

1. **PK/FK picker parity.** Both PK and FK expansion now use the same server-side relationship-selection workflow. Zero matches shows a notice, one match loads directly, and multiple matches open the existing checkbox menu (select all, individual constraints, cancel/apply). PK selects child tables to the right; FK selects parents to the left. The menu is positioned under the correct PK or FK button. No fictitious relationships are inferred.
2. **Click and inspection.** A single click selects a table without loading its design. Ctrl/Cmd+click toggles multi-selection and dismisses any open design inspector. Two consecutive unmodified clicks on the same table (within 450 ms and near the same pointer position) open the top-right inspector. Custom pointer detection is deliberate: the DOM redraws its table cards upon each selection, so browser-native `dblclick` would not reliably fire. Clicking empty canvas clears selection and closes details. The close button no longer unexpectedly wipes selection.
3. **Tool eligibility.** Focus-neighbors is enabled for **exactly one** selected node; two-node path highlighting is enabled for **exactly two**. The underlying JS operations enforce the same cardinality even if called outside the toolbar. Applies to both graph renderers.
4. **Shift indicator.** `graph-interaction-support.js` is shared by both graph renderers. Holding Shift adds a small, temporary `⇧` badge to each *enabled* path/focus button, without latching state. Releasing Shift or blurring the window hides it. Shift+click continues to isolate the respective subgraph.
5. **Undo/redo.** Database Inspector now has undo/redo toolbar buttons and Ctrl/Cmd+Z, Ctrl/Cmd+Y, Ctrl/Cmd+Shift+Z shortcuts when the graph has focus. The bounded 80-step renderer journal covers selection, focusing, isolation, node dragging, camera movements, zoom, reset, and PK/FK graph expansions (including loaded markers). A topology undo/redo is synchronized back to the Blazor page via `DatabaseGraphSnapshot`, with no new SQL query. New edits discard the redo branch. Changing the focused-table roots starts a new journal; switching databases also starts fresh. Inspection details close on undo/redo rather than restoring stale SQL design contents. No state is persisted to the database.
6. **Reset = view tidy.** Reset removes temporary highlighting, compacts the layout of currently **visible** nodes and refits their camera. It **does not** reveal hidden nodes, clear an isolation, collapse branches, alter loaded PK/FK state, wipe selection, or mutate the SQL graph model. This contract is applied to the shared Source Explorer Reset, too.
7. **Selector size and typography.** Database and focused-table `AppDropdown` triggers now share a fixed 38px border-box height. The global **گمرک / سیناپس** pill explicitly uses the already-bundled **Yekan Bakh** Persian font instead of a Latin-only override, with readable 12px sizing.
8. **Cache updates.** The changed ES modules and CSS have new `2.51` cache versions; the shared workspace theme/toolbar continues to come from the existing scoped component bundle.

## Files to review

- `Components/Pages/DatabaseInspectorPage.razor` — reusable relation picker for both PK/FK, server-side history restore and updated UX copy.
- `Components/Common/DatabaseGraphView.razor`, `DatabaseGraphModels.cs` — history enablement, callbacks and snapshot contract.
- `Components/Common/GraphWorkspace.razor`, `.razor.css` — optional history buttons and Shift badges.
- `Components/Common/SourceGraphView.razor` and `wwwroot/js/source-relation-graph.js` — shared focus eligibility, Shift badges, view-only reset.
- `wwwroot/js/database-inspector-graph.js` — double-click, history, view reset, PK/FK popup anchoring.
- `wwwroot/js/graph-interaction-support.js` — reusable bounded graph journal and Shift-key indicator subscription.
- `wwwroot/css/database-inspector.css`, `wwwroot/css/operations-refinements.css`, `Components/App.razor` — selector sizing, Persian typography, cache versions.
- `tests/DatabaseInspectorGraphBrowser/test_graph_251.py` — new browser regression suite. Existing tests updated to the new Reset/focus behavior.

## Verification (Chromium, synthetic SQL metadata)

```bash
python tests/DatabaseInspectorGraphBrowser/test_graph_251.py
python tests/DatabaseInspectorGraphBrowser/test_graph.py
python tests/GraphInteractionBrowser/test_interactions.py
node --experimental-default-type=module tests/GraphTraversalRegression/test_routes.mjs
```

All four suites passed in the packaging environment, as did `node --check` on the changed JS modules. The browser tests do **not** require an SQL Server instance or an authenticated app server.

### Important manual verification on the real app

Open `/tools/database-inspector`, choose `CENOPS_GraphLab_Deep`, select `core.Tenant`, and expand PK: with multiple children, the PK checkbox picker should appear next to the PK button. Expand FK on a child and check the parent picker. Click once to select, double-click to open details, then Ctrl+click a second table to close details and disable Focus. Hold Shift to see the badges and isolate with the path/focus buttons. Test undo/redo across an actual PK expansion, then isolate a branch and press Reset; hidden tables must remain hidden. Check fullscreen and the two dropdown heights.

**Build limitation:** .NET 8 SDK and SQL Server are not installed in this execution environment, so the Razor/C# solution was not compiled or run against a live server here. Run `dotnet build` and your usual end-to-end SQL smoke test on the development machine.
