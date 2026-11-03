# GLASSWORK 2.55 — Graph image export and reusable nested-menu drawer

Route: `/tools/database-inspector`  
Baseline: `NTSW_CENOPS_Glasswork_2_54.zip`  
Scope: shared `GraphWorkspace`, new reusable menu component, reusable image-export engine, Database Inspector graph renderer. **No migrations or SQL changes.**

## Patch notes

1. **iOS/macOS-style share control:** Added an outlined Share icon at the far right of the existing bottom-left graph toolbar. It is opt-in via `GraphWorkspace.ShowExport`, so Source Explorer remains unaffected.
2. **Reusable nested menu:** New `AppNestedMenu` / `AppNestedMenuItem` accepts typed menu commands, nested choices, a custom trigger fragment, disabled entries, and an `OnSelected` callback. Hovering, focusing, or clicking a parent expands a side-by-side drawer. The popover anchors itself away from viewport edges; Escape and outside clicks dismiss it. The positioning/keyboard listener is encapsulated in `app-nested-menu.js` and detached on disposal.
3. **Export actions:** *خروجی تصویر* opens **PNG**, **SVG**, and **WebP** formats. *خروجی کوئری* is intentionally visible but disabled/marked *به‌زودی* until its behavior is designed in a later version.
4. **Full-graph export:** New framework-independent `graph-export.js` accepts nodes, world coordinates, SVG relationship paths, graph sizing, padding, branding and logo. Export bounds are computed from the union of all currently loaded nodes, **not** the viewport, the camera, fullscreen state or the theoretical infinite graph field. The graph is not mutated and its Undo/Redo history does not change.
5. **Branded, framed artboard:** 148 world-unit padding (including curved connectors), subtle 22px dot grid, softly rounded clip/border, and a reserved 93px footer. The footer contains **the actual cyan Skyline logo** (cloned from the layout's existing SVG) on the bottom-right; Persian `گمرک · سیناپس` appears to its left with `CENOPS · GLASSWORK 2.55` underneath. **CENOPS is the product's English name, not “Synapse.”** A fallback mark is available in isolated environments lacking the Skyline shell.
6. **Resolution and size:** SVG is resolution-independent, with escaped table/schema labels and no foreignObject dependency. PNG and WebP are drawn from the vector export at up to 2.4× output scale, clamped to 8192px on the longest dimension and 16 megapixels. WebP is quality `.91`; PNG is lossless. These constraints avoid enormous images for large graphs while preserving sharp small exports.
7. **New-tab preview:** The export handler opens a blank tab **synchronously with the actual browser click** (required for popup blockers when Blazor Server events are asynchronous), then directs it to the generated Blob URL. Errors use the standard Persian system toast; exporting an empty graph explains why no image was generated. Blob URLs are released after a 10-minute preview grace period. Existing PK/FK interactions and graph history remain unchanged.

## Architecture and developer entry points

| File | Responsibility |
|---|---|
| `src/HealthMonitoringApp.WebUI/Components/Common/Menus/AppNestedMenuItem.cs` | Typed nested menu contract: `Value`, `Label`, `Hint`, `Children`, `Disabled` |
| `src/HealthMonitoringApp.WebUI/Components/Common/Menus/AppNestedMenu.razor` | Generic render/action state, hover/focus/click drawer, Blazor event callback |
| `src/HealthMonitoringApp.WebUI/Components/Common/Menus/AppNestedMenu.razor.css` | Shared modern menu styles, drawer animation, responsive sizing |
| `src/HealthMonitoringApp.WebUI/wwwroot/js/app-nested-menu.js` | Menu edge positioning; global outside/Escape listeners and cleanup |
| `src/HealthMonitoringApp.WebUI/wwwroot/js/graph-export.js` | `buildGraphSvg(options)` and `renderGraphBlob(options, format)`; pure graph geometry + browser raster bridge |
| `src/HealthMonitoringApp.WebUI/Components/Common/GraphWorkspace.razor` | Optional rightmost Share trigger and default export menu (overridable via `ExportMenuItems`) |
| `src/HealthMonitoringApp.WebUI/Components/Common/DatabaseGraphView.razor` | Enables export and routes errors through `AppNoticeService` |
| `src/HealthMonitoringApp.WebUI/wwwroot/js/database-inspector-graph.js` | Captures current topology and Skyline logo; opens a new tab before generating the image |
| `tests/DatabaseInspectorGraphBrowser/test_graph_255.py` | Chromium verification for pure SVG, actual logo, all formats, new tabs and UI |

### Reuse the exporter

Supply `{ nodes: [{ id, schema, name, x, y, pkLoaded, fkLoaded }], edges: [{d,color}], nodeSize, padding, branding, logo }` to `buildGraphSvg` or `renderGraphBlob`. Positions are world coordinates, not pixels from the screen. Relationships use the same SVG `d` coordinates as their renderer (so export matches visible graph geometry). The image exporter has **no hard dependency on Database Inspector or Blazor**; other graph consumers can provide their own layout, colors and brand metadata.

### Reuse the nested-menu drawer

Drop `AppNestedMenu` into another Razor view, pass `Items` as a list of `AppNestedMenuItem`, provide a `TriggerContent` fragment and `OnSelected` callback. An item with `Children` automatically becomes a parent drawer action. Disabled items remain visible but cannot execute. The component owns its close, orientation, keyboard and pointer behavior.

## Regression results

Run from project root (requires Python Playwright + Chromium and Node.js):

```bash
python tests/GraphInteractionBrowser/test_interactions.py
python tests/DatabaseInspectorGraphBrowser/test_graph.py
python tests/DatabaseInspectorGraphBrowser/test_graph_251.py
python tests/DatabaseInspectorGraphBrowser/test_graph_252.py
python tests/DatabaseInspectorGraphBrowser/test_graph_253.py
python tests/DatabaseInspectorGraphBrowser/test_graph_254.py
python tests/DatabaseInspectorGraphBrowser/test_graph_255.py
node tests/GraphTraversalRegression/test_routes.mjs
```

All seven browser suites pass, as well as **4,418 graph route cases**. The previous browser fixture loaders were updated to inline the new dependency when executing Blob-import test modules. The test for 2.55 also checks real Skyline SVG artwork, XML validity, escaped SQL names, a node outside the viewport, PNG/WebP signatures, and that each export opens a real tab. JS syntax checked. No .NET SDK or SQL Server is installed in the artifact environment; **full Blazor build and application integration require local verification**.

## Manual acceptance checklist

- Expand a large graph; move some nodes off screen and zoom in. Export SVG, PNG and WebP. Each opens in a new tab with **all** nodes and relationships, plus a tight branded background frame.
- Inspect the rounded edges, dot pattern, original cyan Skyline logo at bottom-right, and `گمرک · سیناپس` / `CENOPS · GLASSWORK 2.55` footer.
- Click the Share action; hover then click Picture Export. Verify drawer width animation, menu item availability, Escape/outside dismissal and RTL layout.
- Try an empty graph and confirm the normal lower-left Persian notice, with no empty new tab.
- Try a browser with popups blocked and confirm the explanatory Persian notice.
- Switch database, check fullscreen, prune nodes, Undo/Redo, and export again. The export must respect the **current topology** without resetting graph state.
- Confirm Query Export cannot yet be selected and has no invented behavior.
