# Glasswork 2.12.6 — Meaningful monitoring visualizations

Based on **Glasswork 2.12.5**. **No EF migration**. No job query, scheduler, existing stored JSON, or access-control changes.

## Semantic contract

Charts keep the original contract: one comparison key (datetime or numeric) and selected numeric measures. Their meanings differ:

| Type | X / grouping | What the chart displays |
| --- | --- | --- |
| Line, area, bar, step, smooth, scatter, etc. | Original comparison key | Values of selected measures at each key |
| Pie / doughnut | Selected measures | One slice **per measure**, based on SUM across all loaded/filter-matching points |
| Treemap | Selected measures | One rectangle **per measure**, area proportional to its SUM |
| Gauge | Selected measures | One arc **per measure**, percentage = SUM(measure) / SUM(all selected measures) × 100; never an invented SLA/target |
| Radar/spider | Selected measures | One spoke **per measure**, shape based on the SUM of that measure, scaled against the largest selected SUM |
| Bubble | Comparison key + selected measures | Each selected measure is a separate color series; X = comparison value, Y = numeric value, radius ∝ sqrt(abs(numeric value)) |

Pie, doughnut, treemap, gauge and radar **require nonnegative values**; they show an explanatory invalid-state message rather than masking negative numbers. Null/missing measurements stay gaps. At least two selected numeric measures are needed for pie/doughnut/treemap/gauge; radar requires three.

**Important:** Composition is statistically meaningful only for measures sharing a unit and without overlap. For a result containing `TotalCount`, `SuccessCount`, `BusinessErrorCount`, and `SystemExceptionCount`, choose the latter **three** for a breakdown. Choosing `TotalCount` as well would double-count the same underlying requests. The report wizard explicitly lets users choose fields, and a warning explains this condition. No implicit field-name guessing or schema-specific exception is baked into the renderer.

## Time windows and data integrity

The expanded report view's date filters apply **before** composition aggregation. The backend deduplicates identical comparison values using the latest collected execution window, and pulls up to 14 daily data buckets from storage. Composition charts **do not use sampling** before summing; they use up to the latest 2,500 deduplicated points. If that safety cap excludes older points, a notice explicitly explains the truncation. More history requires a future server-side aggregated report endpoint, not silently extrapolated values.

`spiral` is still intentionally absent. A truthful spiral requires defining cycle length/phase (e.g., a day or week). The one-key/numeric-measures contract does not say what cycle the data represents.

## Fixes in this version

1. Gauge arc uses one fixed semicircle path and `pathLength` + stroke dash array. SVG height/width and card clipping are bounded; the previous overshooting arc no longer happens.
2. Pie/doughnut/treemap/gauge/radar operate on explicit selected **measure fields**, no dates-as-slices or dates-as-radar spokes.
3. Radar labels show full field identifiers when possible; the SVG's marker titles expose exact name/value.
4. Bubble draws each measure in a separate stable palette color and a shared radius scale. Legend names identify the series.
5. Create/edit wizard now supports selecting which numeric columns to include, persists selection, and validates minimum field counts server-side.
6. Composition uses the whole bounded retained point set instead of sampling before SUM.

## Suggested manual tests

1. Run the SQL that returns hourly `TotalCount`, `SuccessCount`, `BusinessErrorCount`, `SystemExceptionCount` and let the job persist some windows.
2. Edit a report, keep `Hour` as comparison, and select `SuccessCount`, `BusinessErrorCount`, `SystemExceptionCount` **only**.
3. Test pie/doughnut/treemap/gauge: for the sample (168, 13, 27), the shares should be **80.77%, 6.25%, 12.98%**, summing to 100%.
4. Test a custom date filter in the expanded modal; the part totals and percentages should change to match the filtered points.
5. Test spider: three spokes should be the selected field names, *not* dates, with marker tooltips reporting exact aggregated values.
6. Test bubble: selected fields should have visually distinct colors, matching the measure legend, for every comparison point.
7. Test all-negative, zero-only and null-containing results; the renderer should show an understandable empty/invalid state, not false percentages.
8. Regression-test legacy line/area/bar, zoom, data badges, modal Escape handling, and job/report management permissions.

## Verification

Static source checks, ZIP CRC checks, and a headless Chromium **isolated gauge layout test** were performed. The fixture tested gauge SVGs at desktop width and reported zero container overflow. **No running Blazor app or .NET compilation** was available in this environment; build and browser integration tests must still be performed locally before deployment.
