# Glasswork 2.48 — Shared Graph Workspace, Database Inspector UX, SQL GraphLab

## Baseline and release scope

Built on **NTSW_CENOPS Glasswork 2.47**. No changes to the frozen Roslyn/Source Intelligence indexer, authorization contracts, database inspector metadata service, or MediatR rules. This release is a **graph UI refactor**, compact layout improvement, and a separate test database script.

## Changes

1. **`GraphWorkspace.razor` + `GraphWorkspace.razor.css` (new)** — shared CENOPS graph workspace. Owns the dot-grid stage, graphical reset/zoom/path/branch/fullscreen toolbar (bottom left), legend (bottom right), top-right inspector slot, and fullscreen-safe modal slot. The child diagram owns data/coordinates/edges and provides callbacks, so the shell is reusable beyond the two existing explorers.
2. **`SourceGraphView.razor`** — uses `GraphWorkspace`, preserving the legacy source SVG renderer, original source-node behavior, source details and file-preview modal. Source semantic indexing is untouched.
3. **`DatabaseGraphView.razor`** — uses **the same GraphWorkspace**; preserves SQL Server FK/PK interactions, multiple-FK chooser, and SSMS-style click-to-inspect panel. Gains the shared SVG toolbar iconography, path highlight and focus controls. Database renderer is intentionally independent of the source tree-layout algorithm (SQL relations allow cycles, self-FKs, shared parents).
4. **`database-inspector-graph.js`** — updates to use the shared stage, adds highlight/focus actions, retains pan/zoom/drag and text-only DOM insertion for database names.
5. **`database-inspector.css`** — adopts Source Explorer's dot-grid/background/corner theme and keeps Database Inspector *page-level scroll disabled*; long inspector details remain scrollable inside the upper-right panel. Responsive viewport available height drives graph height instead of a hardcoded 67vh.
6. **`NavMenu.razor` + `MainLayout.razor`** — tool's sidebar title and page breadcrumb changed to **بررسی دیتابیس**, and Persian subtitle added.
7. **`Skyline.razor.css`** — both plus/create and tools dropdowns flow column-first, **maximum six entries per column**. A seventh starts a new RTL column; additional items create more columns, with horizontal overflow on narrow screens. Empty/inaccessible items do not take up a grid cell.
8. **`App.razor`** — updated Database Inspector stylesheet cache version.

### Why the graph renderers remain separate

**Shared:** presentation shell, controls, canvas, corner overlays, fullscreen and consistent interaction affordances. **Different:** `source-relation-graph.js` uses a rooted dependency hierarchy (potentially collapsible); `database-inspector-graph.js` uses graph edges and table IDs, including cycles and multi-parent nodes. Forcing them through one hierarchy algorithm would reintroduce inaccurate edges. Future diagrams can reuse `GraphWorkspace` by providing their own renderer and callbacks.

## SQL Server GraphLab test database (separate SQL file)

`tools/database-inspector/CENOPS_GraphLab_Complex_Schema.sql`

Available separately with this release as `CENOPS_GraphLab_Complex_Schema.sql`.

- Creates a **new** SQL Server database: `CENOPS_GraphLab` (never touches CENOPS's `Default` database).
- Creates **34 tables**, **73 foreign keys**, and **six schemas**: `core`, `hr`, `sales`, `inventory`, `logistics`, `audit`.
- Deliberately includes composite FKs, many FKs from one table, multiple references to the same table, self-references, two cycles (`Department ↔ Employee`, `Refund ↔ ReturnAuthorization`), shared ancestors, nullable FKs, check/default/unique/computed/identity properties, a filtered index, and an **unlinked** import table that must not display invented FK lines.
- Schema only: no business rows, credentials, or seed data are needed for testing catalog diagrams.
- The script refuses to replace an existing GraphLab schema. Run once, on a nonproduction SQL Server **with CREATE DATABASE permission**.

### Using GraphLab with Database Inspector

1. Run the SQL script in **SSMS** with a test-server connection; it creates `CENOPS_GraphLab` and reports the table/FK inventory.
2. Set up a **new external connection-string name**, for example `GraphLab`, under `ConnectionStrings` (pointing at `Initial Catalog=CENOPS_GraphLab`). Never change or reuse the CENOPS `Default` connection. If you explicitly set `DatabaseInspector:ConnectionNames`, include `GraphLab` in that list.
3. Give the connection login enough SQL metadata visibility (`VIEW DEFINITION` as appropriate for the environment). Prefer read-only metadata permissions over data access.
4. Launch **ابزارها → بررسی دیتابیس**, choose `GraphLab`, select `sales.SalesOrder`, and click **FK**: multiple references should open the checkbox chooser; verify child->parent edges appear to the left for chosen relationships.
5. Click **PK**: incoming references such as `sales.SalesOrderLine`, `sales.Payment`, `sales.SalesOrderComment`, and `logistics.Shipment` should fan out to the right.
6. Choose `hr.Employee` and traverse `hr.Department` manager/department references to verify cycles are linked without duplicate SQL table nodes.
7. Choose `inventory.StockBalance`, expand **PK** to `inventory.StockMovement`, and inspect the **three-column** composite foreign-key mapping.
8. Choose `audit.UnlinkedImportStage`: no FK/PK relation should be invented merely because it has a `TenantId` column.
9. Click a table to inspect column metadata in the **top-right**, without a hover tooltip.

### Environment limitations

The script requires Microsoft SQL Server and SSMS/sqlcmd support for `GO` batch separators. **It has not been executed against a live SQL Server in this environment.** Review/use it only on a test server. The backend reads actual `sys.foreign_keys` metadata, not assumptions based on table or column names.

## Build and test

```powershell
# At repository root
dotnet restore
dotnet build NTSW_CENOPS.sln
```

This release **does not introduce a new CENOPS EF migration**. If your 2.47 database-inspector access migration was not applied yet, apply it before testing the page.

**Manual acceptance checklist:**

- `/tools/source-explorer`: dot background, toolbar and modal continue functioning; node expansion still uses exact source symbols.
- `/tools/database-inspector`: same shared background/control positioning; 100% available workspace height; no outer scrollbar even with many graph nodes. Only inspector panel / FK popover may internally scroll.
- Database Inspector: PK→right; FK→left; multiple FKs open checkboxes and select-all; self FK/cycle/composite FK display.
- Database Inspector: no tooltip on hover; click opens design panel at top-right, close works.
- Graph reset, zoom, drag, pan, path highlight, branch focus and fullscreen work in both contexts.
- Sidebar text **بررسی دیتابیس**; Skyline plus/tools menus split after six visible items.

## Validation performed here

- Chromium smoke tests for Source Explorer JS in the shared workspace, Database Inspector node actions, highlight/focus, and HTML-safe text rendering.
- Chromium tests for Skyline menus (13 entries → 6+6+1 columns) and no outer scroll at desktop viewport sizes.
- Static JavaScript syntax and CSS parsing; ZIP integrity/content checks.
- **Not performed:** full C# / Razor compilation (no .NET SDK in execution environment), live SQL Server execution, EF migration, application login and end-to-end Blazor browser test.

## Operational notes

This UI release doesn't change the backend's allowed external connection list or read-only query behavior. Connection strings must stay server-side; don't expose them in page models, browser logs, or the SQL script. Graph work is scoped to the new interface and does **not** resume Source Intelligence development.
