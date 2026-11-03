# C.E.N.O.P.S. 2.61 — GLASSWORK

## About dialog — edge-to-edge brand layout

This patch follows the approved About dialog reference while preserving the existing shared dialog architecture and role visibility.

- Added opt-in `FlushContent` to `AppModal`, default `false`. Only the CENOPS About dialog opts in. It removes modal shell padding and keeps overflowing content scrollable **within** the rounded dialog on short displays. All other dialogs keep their default spacing.
- Extended the existing cyan CENOPS mark into a full-height right column with the same background color as the mark itself. Only its top-right corner is rounded, matching the modal silhouette. The mark stays aligned to the height of the details column.
- The left details column now owns its own padding without an extra right-side inset; a grid column gap gives the mark and text consistent separation.
- Removed vertical spacing between the two Persian legal paragraphs, while retaining their content and the dividing line above them.
- Removed the footer's top margin, so its divider follows the grid immediately, and gave the footer intentional bottom padding.
- Kept the copyright line, the supplied ILECY logo, and the blue ILECY signature exactly as before.
- Maintained narrow-screen stacking and keyboard/modal accessibility.
- Updated version metadata and existing graph image-export watermark to **2.61**; release codename remains **GLASSWORK**.

## Affected source

- `src/HealthMonitoringApp.WebUI/Components/Common/AppModal.razor`
- `src/HealthMonitoringApp.WebUI/Components/Common/AppModal.razor.css`
- `src/HealthMonitoringApp.WebUI/Components/Common/CenopsAboutDialog.razor`
- `src/HealthMonitoringApp.WebUI/Components/Common/CenopsAboutDialog.razor.css`
- `src/HealthMonitoringApp.WebUI/Components/Common/CenopsRelease.cs`
- `src/HealthMonitoringApp.WebUI/Components/App.razor`
- `src/HealthMonitoringApp.WebUI/Components/Common/DatabaseGraphView.razor`
- `src/HealthMonitoringApp.WebUI/wwwroot/js/database-inspector-graph.js`
- `src/HealthMonitoringApp.WebUI/wwwroot/js/graph-export.js`
- `tests/AboutDialogBrowser/test_about_261.py`
- `tests/DatabaseInspectorGraphBrowser/test_graph_255.py` (watermark expectation)

## Verification

```bash
python tests/AboutDialogBrowser/test_about_261.py
python tests/DatabaseInspectorGraphBrowser/test_graph_255.py
node tests/GraphTraversalRegression/test_routes.mjs
```

The About regression verifies the flush class, modal bounds, brand background/corners, equal desktop heights, tight legal paragraphs, footer spacing, blue ILECY signature, and small-screen layout. Browser snapshots were inspected against the supplied reference.

A `dotnet build` and live interactive modal test should be performed in the target developer environment; the .NET SDK is unavailable in this workspace.
