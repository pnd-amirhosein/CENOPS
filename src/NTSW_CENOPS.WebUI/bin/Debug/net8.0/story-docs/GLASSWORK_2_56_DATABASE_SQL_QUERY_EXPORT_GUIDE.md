# GLASSWORK 2.56 — Database Inspector SQL query export and click-only drawer

**Baseline:** `NTSW_CENOPS_Glasswork_2_55.zip`  
**Page:** `/tools/database-inspector`  
**Brand:** CENOPS (not the English word “synapse”).

## Changes

1. **Click-only expandable menu.** `AppNestedMenu` now expands/collapses a parent only upon clicking it; hover and focus do not mutate the drawer's expansion state. A second click collapses it, and clicking a different parent opens that drawer. The existing Escape, outside-click, submenu selection, responsiveness, and viewport alignment remain intact. This eliminates the mouse-hover/height feedback loop.
2. **Query export activation.** The Export toolbar's **خروجی کوئری** parent is disabled until one or more graph nodes are selected (including Ctrl+click and Ctrl+drag). Selection is copied immutably when requesting a preview. Image export stays available independently.
3. **SELECT query.** Generates one stand-alone `SELECT` per selected table. All columns come from the metadata service in column ordinal order, with explicit table aliases and unique column aliases; no `SELECT *`.
4. **INNER JOIN query.** Builds connectivity from authoritative FK metadata for selected tables, not only already-expanded graph edge lines. Each connected component produces **one** statement, using a deterministic spanning-tree sequence of `INNER JOIN`s with SQL Server column mappings (including composite keys). Disconnected components and isolated tables produce separate statements. Unselected intermediary tables are never brought into the SQL implicitly. A spanning tree avoids joining a table twice when the selected tables have cycles or multiple competing FK paths; where alternative FK paths exist, this is a useful preview **not** a claim that every FK constraint was joined simultaneously.
5. **SQL identifier safety.** All schema, table, column, and alias identifiers use SQL Server bracket quoting and escaping (including embedded `]`). The generated text is **never executed**. No credentials are sent to JS. JS only formats metadata already authorized/loaded through `IDatabaseInspectorService`.
6. **Shared modal and handoff.** `AppModal` shows the SQL preview inside the graph's fullscreen ancestor, with **Copy** and **باز کردن در پاک‌سازی کد** actions. The latter uses the existing one-time `cenopsSnippetDraft` session-storage token and navigates to `/tools/clean-code?draft=...`, which loads the code into the existing `NoteSnippetBox` editor with SQL language selected. SQL text is not in the address bar.
7. **Export watermark** updated to **CENOPS · GLASSWORK 2.56**. The database-inspector stylesheet cache key was updated to deliver the new preview styles.

## Files

| Path | Purpose |
|---|---|
| `Components/Common/Menus/AppNestedMenu.razor` | Reusable click-only drawer behavior |
| `Components/Common/GraphWorkspace.razor` | Updated disabled default query hint |
| `Components/Common/DatabaseGraphView.razor` | SQL submenu, enabled state based on selected node IDs, export callback |
| `Components/Common/DatabaseGraphModels.cs` | Immutable `DatabaseQueryExportRequest` |
| `Components/Pages/DatabaseInspectorPage.razor` | Metadata loading and caching, SQL preview modal, copy, Clean Code handoff |
| `Components/Pages/CodeCleanupPage.razor` | Consume existing one-time SQL draft and populate editor |
| `wwwroot/js/database-query-export.js` | Pure, reusable, metadata-based SELECT/JOIN statement formatter |
| `wwwroot/js/database-inspector-graph.js` | 2.56 image watermark |
| `wwwroot/css/database-inspector.css` | SQL modal preview styles |
| `Components/App.razor` | CSS cache key for preview styling |

## Test commands

```sh
node --experimental-default-type=module --test tests/DatabaseInspectorGraphBrowser/test_query_256.mjs
python tests/DatabaseInspectorGraphBrowser/test_query_transfer_256.py
python tests/DatabaseInspectorGraphBrowser/test_graph.py
python tests/DatabaseInspectorGraphBrowser/test_graph_251.py
python tests/DatabaseInspectorGraphBrowser/test_graph_252.py
python tests/DatabaseInspectorGraphBrowser/test_graph_253.py
python tests/DatabaseInspectorGraphBrowser/test_graph_254.py
python tests/DatabaseInspectorGraphBrowser/test_graph_255.py
python tests/GraphInteractionBrowser/test_interactions.py
node --test tests/GraphTraversalRegression/test_routes.mjs
```

The SQL generator's test cases include explicit projection, two disconnected join groups + isolated table, multi-column FK, unselected intermediates, cycles, duplicate relation alternatives, SQL Server `]` escaping, and invalid metadata. The draft storage smoke test mocks session storage because network navigation is blocked in the automated browser sandbox; it checks the real helper's one-shot token behavior, **not** a full Blazor navigation. Existing graph browser regressions cover history, selection, removal, and image export.

## Manual acceptance checklist (requires running .NET + SQL Server)

- [ ] Start with no selection: **خروجی کوئری** is disabled; PNG/SVG/WebP remain accessible. Click on an empty graph and verify query command disables again.
- [ ] Open/close image drawer repeatedly with click; simply moving the pointer over it does not expand/collapse or jitter.
- [ ] Select one table, export each query mode; verify explicit columns and no unnecessary join.
- [ ] Select related tables across one or more FK steps; verify correct multi-column `ON` and aliases.
- [ ] Select disconnected pairs plus an isolated table; verify independent statements separated by semicolons.
- [ ] Verify Copy puts the entire query in clipboard (HTTPS/localhost recommended).
- [ ] Verify **باز کردن در پاک‌سازی کد** populates the SQL editor, including after using fullscreen graph mode.
- [ ] Verify image export still opens in a new tab and contains **GLASSWORK 2.56** watermark.

**Environment note:** JavaScript/Chromium regression suites passed. The .NET SDK and live SQL Server are unavailable in the packaging environment, so final Blazor compilation and real-database integration require a local build/test.
