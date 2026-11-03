# Glasswork 2.39 — Source Explorer and Reusable Dependency Graph

Baseline: **Glasswork 2.38**. This update does not change exact-match declaration resolution, repository registration, Git behavior, or the permission policy for reading source files.

## Changes

1. `/tools/source-explorer` has substantial bottom breathing room (`clamp(130px, 32vh, 440px)`) so the reference workspace is easier to navigate.
2. The editor view uses an HTML-escaped, token-aware C# highlighter (`SourceSyntaxHighlighter`). Both the short preview and the virtualized full-file view share the same palette. The code remains read-only, not executed or inserted as untrusted HTML. Persian strings/comments remain source text rather than reversals caused by a visual transform.
3. Definition results and metadata are more compact, with stronger typography and less neutral gray. Repeated exact-query names are not used as the primary title for every result: type, containing symbol, project, source file, and line provide distinguishing information.
4. The definitions column has a small **local filter** for project, file, symbol kind, namespace, containing symbol, or registered source. It operates on the currently loaded page of exact results (not a new fuzzy server search). The exact server query and its page/category counts are unchanged.
5. The search accordion and two-column inspector are retained. References remain full width below them.
6. `SourceRelationGraph` is a reusable Blazor component backed by a small self-contained SVG interaction module, without CDN libraries. The reference workspace offers **Tree** and **Graph** tabs. Graph nodes represent the selected declaration, files, and actual one-hop reference occurrences. Connection lines are visual grouping relationships, not recursive code-call analysis.
7. In Graph: hover reveals metadata; click opens an upper-right inspector with project, path, line, optional namespace, and approximately three source lines. Usage-node **View details** opens the existing authorized modal, whose **Full file** action is preserved.
8. Graph supports node dragging, blank-canvas panning, mouse-wheel zoom, +/- zoom buttons, reset (including node arrangement), fullscreen, and keyboard Enter/Space selection on focusable nodes. Listeners are removed on component disposal.
9. The ESC close hint for `AppModal`'s optional `FloatingClose` now sits **inside** the modal at top-left. This also affects the chart modal that uses the same optional close variant.
10. Source-use records gain an **optional** `Namespace` member. Newly indexed references populate it from Roslyn semantic context; old index snapshots remain readable, with `Not indexed` shown for unavailable namespace data.

## Reusable files

- `Components/Common/SourceGraphModels.cs` — `SourceGraphNode`, `SourceGraphEdge` independent from Roslyn/index storage.
- `Components/Common/SourceGraphView.razor(.css)` — selectable, resizable, interactive graph surface and inspector; takes nodes, edges and `OnOpenDetails`.
- `wwwroot/js/source-relation-graph.js` — SVG layout, pointer events, zoom, fullscreen, tooltips, resource cleanup. Does **not** parse code or fetch source files.
- `Components/Common/SourceSyntaxHighlighter.cs` — HTML-escaped token generation; no embedded scripts or unsafe source markup.
- `wwwroot/css/source-syntax.css` — source-viewer token theme used across both source previews.

## Security and scope

- Graph data comes from the **same authorized** `GetDeclarationAsync` / `GetReferencesAsync` calls as the existing tree.
- Only previously authorized `GetSourceFileAsync` can load complete files. The graph JS never reads server paths or invokes HTTP itself.
- The highlighter HTML-encodes every token before applying predefined CSS classes.
- Graph truncates at **280 occurrence nodes** for responsiveness and displays a visible warning; the complete occurrence set remains in the tree tab.
- This is a **one-hop occurrence visualization**. The file nodes organize occurrences by source location, not method-to-method call flow.
- C# coloring uses lightweight heuristics, not a full semantic syntax theme. It is deliberately safe and dependency-free.
- Local definition filtering applies to the 80-result page already loaded; use pagination to inspect additional exact definitions.

## Upgrade

1. Replace files from the changed-files ZIP, or use the full release.
2. Run `dotnet restore` and `dotnet build` locally.
3. Your old index remains compatible; **reindex only if you want namespace metadata for usage nodes**.
4. No EF migration, package installation, or new appsettings key is needed.

## Local acceptance checklist

1. Search `BillOfLading`; ensure the original exact declarations and categories are unchanged.
2. Filter results by `Project`, filename, or symbol kind; changing the local filter must not change server search or selected source location.
3. Confirm compact details, readable C# syntax colors, proper Persian text, and the lower page gutter.
4. Switch Tree → Graph → Tree without losing selected symbol; graph should display distinct file branches even with similar usage counts.
5. Hover node; click usage; verify project, file, line, namespace and short context; open existing detail modal, then expand to full file.
6. Drag a node, pan the canvas, scroll to zoom, zoom via buttons, reset layout, and enter/exit fullscreen. Check touch/pointer interaction if applicable.
7. Check graph on desktop and small displays; inspect a symbol with more than 280 usages to verify the truncation notice and complete tree.
8. Test ESC modal placement and actual Escape/backdrop behavior with source and report/chart dialogs.
9. Reindex a repository and confirm `Namespace` in graph usage inspector, including global-namespace and older-index cases.
10. Test private source repository access using an account without the `tools.source-intelligence` permission; full-file access must remain denied.

## Verification performed here

- Browser-based Chromium fixture: node selection, drag, pan/zoom/reset, wheel zoom, fullscreen, file-node layout, and no page JavaScript errors.
- JavaScript syntax and modified stylesheet parsing; project XML and package integrity.
- Targeted source-contract checks for existing exact-match search, callback wiring, source authorization boundary, and namespace metadata.

**Not verified:** actual .NET 8/Razor compilation, real authentication/index/database integration, and production browser behavior. The .NET SDK is not installed in this environment. The graph browser fixture tests its JavaScript and CSS in isolation, not a running Blazor application.
