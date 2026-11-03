# Glasswork 2.19 — Freeform Dashboard Layout

**Baseline:** `NTSW_CENOPS_Glasswork_2_18.zip`  
**Scope:** the dashboard canvas and geometry persistence. Dashboard access control, shared selection/default behavior, content picker, previews and reports remain in place.

## 1. User-facing behavior

- **Pixel-precision resize:** drag the compact handle centered on a card's **left edge** to resize width, on the **bottom edge** to resize height, or on the **bottom-left corner** to resize both. There are no column/row increments. A 13px gesture changes width by 13px at the current viewport size.
- **Improved handles:** discreet pills with a larger invisible hit target and a subtle corner grip. They appear on hover/focus when editing; touch scrolling is disabled on the grips themselves.
- **Room for editing:** `40vh` of temporary blank space *after* the dashboard while editing. Disappears when edit mode ends; the space is not saved.
- **Freeform layout:** cards have independent right/top/width/height. Different-height neighbors **do not have to align to any row**; leaving arbitrary gaps is intentional.
- **Drag/swap:** grab the six-dot button. Dropping near the **center** of another card swaps their **positions**, not their sizes. Cards keep their original dimensions. A small positional correction may be applied if differently sized cards would otherwise overlap after the swap.
- **Move/insert:** drop over a card's **top, bottom, left or right edge** to move adjacent to that edge. Drop into **empty canvas space** to place at the actual pointer offset, without snapping.
- **Collision behavior:** as a resize/move completes, intersecting cards are moved down only as far as necessary. Unaffected cards keep their existing positions/dimensions; different-height rows don't become aligned.
- **SQL/cURL:** the description remains a short, fixed-height introduction; the code preview stretches into the remaining available height.
- **Reports:** retain their existing chart component. Its parent resizes with the card and scrolls its content if it cannot fit.
- **Keyboard:** focus the move grip and use `Alt + Arrow` to shift a card by 10px; add `Shift` for 1px steps. On a resize grip, use the corresponding arrow keys (10px; `Shift` = 1px).
- **Small screens:** screens at or below 620px use a readable stacked list. This is a presentation fallback, not a rewritten layout. Editing titles/content there does **not** overwrite desktop card coordinates; drag/resize handles are hidden at that breakpoint.

## 2. Files changed

| File | Change |
|---|---|
| `src/HealthMonitoringApp.WebUI/wwwroot/js/dashboard-grid.js` | Pixel canvas controller (retains filename to avoid unrelated script changes); pointer moves/resizes, center swaps, directional edges, collision resolution, old-layout fallback, Blazor callbacks, keyboard control. Public browser API now `cenopsDashboardCanvas.mount` and `.snapshot`. |
| `src/HealthMonitoringApp.WebUI/Components/Pages/Home.razor` | Mounts read-only and editable canvases; one geometry callback per completed gesture; snapshots geometry before saving; edit-mode bottom spacer; Razor-owned drop indicators. |
| `src/HealthMonitoringApp.WebUI/Components/Pages/Home.razor.css` | Independent-position canvas, drop ghost, 40vh spacer, readable pre-initialization fallback, mobile stack. |
| `src/HealthMonitoringApp.WebUI/Components/Pages/DashboardTile.razor` | Per-card position metadata in attributes and pointer-enabled drag grip; removes native HTML drag-and-drop. |
| `src/HealthMonitoringApp.WebUI/Components/Pages/DashboardTile.razor.css` | Absolutely positioned card, new edge handles, expandable SQL/cURL code previews, hover markers. |
| `src/HealthMonitoringApp.WebUI/Components/App.razor` | Cache busting `dashboard-grid.js?v=2.19`. |
| `src/HealthMonitoringApp.Application/Dashboards/DashboardContentModels.cs` | Adds optional pixel geometry to the persisted reference model. |
| `src/HealthMonitoringApp.Infrastructure/Dashboards/DashboardConfigurationService.cs` | Recognizes and strictly validates new geometry fields before saving. |

## 3. Layout JSON and backwards compatibility

Each `DashboardContentReference` still has `type`, `id`, `w` and `h`. The **optional** new fields are:

```json
{
  "type": "sql",
  "id": 52,
  "w": 3,
  "h": 5,
  "x": 240,
  "y": 90,
  "pw": 430,
  "ph": 275,
  "cw": 1140
}
```

- `x`: horizontal distance **from the right edge** of the canvas, in pixels.
- `y`: distance from the top of the canvas, in pixels.
- `pw`, `ph`: card width and height in pixels.
- `cw`: width in pixels of the canvas when the geometry was saved. Allows proportional horizontal scaling on desktop browser resize; the saved data is not mutated until the user edits/saves.
- `w`, `h`: retained for **2.18 and earlier** layouts. Missing/all-zero pixel fields mean an old/unplaced card. The browser computes an initial **non-overlapping** position and upgrades it to pixel geometry when an editable dashboard is saved.

No EF schema or database migration is required: layouts are still stored in existing `LayoutJson` string columns.

## 4. Implementation and interaction lifecycle

1. On render, both global and selected user dashboard zones call `cenopsDashboardCanvas.mount(grid, dotnetRef, zone, editable)`. Each canvas has an independent `WeakMap` controller.
2. For saved pixel layouts, horizontal measurements are scaled by `currentWidth / cw`; heights and vertical offsets remain in pixels. Legacy/new cards fill the earliest available exact-pixel gap.
3. The six-dot grip starts a **pointer-captured** move. Resizers update DOM widths/heights on **every pointer movement**, without .NET round trips or snapping.
4. Hovered target **center** means swap. Hovered target **edge** means adjacent placement. Otherwise drop coordinates are the dragged card's actual cursor delta. Preview indicators show which will happen.
5. When a gesture ends, collision resolution shifts only overlapping neighbors down, then sends a single list of geometry updates via `ApplyTileGeometryAsync` to the page. No content is persisted until the user chooses **ذخیره**.
6. Before saving, `SyncCanvasAsync` takes an authoritative client snapshot (including untouched old-format tiles). On **انصراف**, original layout JSON is parsed again; unsaved movements disappear.
7. All service authorization checks for global/personal dashboard changes are unchanged. The service validates bounds and rejects invalid/partial position objects.

## 5. Local verification checklist

1. **Build the application**: `dotnet build NTSW_CENOPS.sln` (restore configured dependencies first). No new migration.
2. Open an **editable** dashboard on desktop, add at least four cards of different types. Resize one left edge by ~13px and one corner by different X/Y distances; verify changes are continuous.
3. Place a 50px-tall card beside two stacked cards of different heights. Check that they **do not share row tracks** and that unused vertical gaps remain possible.
4. Drag the six-dot grip to the center of a different-sized card: their positions exchange, while **each card's dimensions stay its own**. Drag to each edge of a different card, and then into empty space; check directional insertion and free placement.
5. Resize an item into its lower neighbor. Check only intersecting items move down, with no unwanted overlap. Move away again and observe that deliberately empty space is retained.
6. Resize both a **report chart** and an **SQL/cURL** card, ensuring chart controls are usable and code fills extra height without stretching the description.
7. **Save**, reload, switch user dashboards and back, then **Cancel** a new edit. Confirm persistence, isolation between global and selected personal zones, and correct cancellation.
8. Check a read-only viewer: saved layout renders but grips are absent. Check global editing only for Owner/Admin and personal editing only for its creator.
9. Test at ~800px and ≤620px. Mobile cards should be readable and editing the dashboard name must **not** rescale persisted desktop geometry.
10. With JavaScript temporarily disabled/blocked, cards should still render in a fallback grid rather than overlapping.

### Checks performed in the packaging environment

- JavaScript parsing via Node.js (`node --check`).
- Browser-based pointer integration fixture in headless Chromium: 13px horizontal resize, 19px/27px corner resize, swap of differently-sized cards, gap drop, bottom-edge drop, collision shift, mobile stacking, and preservation of desktop geometry when viewing at mobile width.
- Source/CSS checks and ZIP integrity checks.

**Not performed in this environment:** .NET compilation and server-side Blazor/EF/SQL integration testing, because this environment has no .NET SDK or SQL Server. The previous version's database migration is still required if it has not already been applied; **this version adds none**.
