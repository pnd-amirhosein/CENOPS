# Glasswork 2.49 — Database Inspector: Multi-table Graph, Toasts and Deep SQL Lab

## Baseline

Built from `NTSW_CENOPS_Glasswork_2_48.zip`. This version does **not** alter the frozen Source Intelligence indexing/semantic reference analyzer, authentication rules, or database schema.

## Features and implementation

### 1. Database Inspector notices

`DatabaseInspectorPage.razor` uses the existing scoped `AppNoticeService` for no-FK/no-child notifications, graph capacity warnings, and connection errors. An `Info(...)` API was added to the notice service with a separate message title.

A second `AppNoticeHost` is rendered *inside* the shared `GraphWorkspace` only for Database Inspector (`ShowFullscreenNotices="true"`). It is hidden in normal mode; when the graph enters the browser fullscreen top layer, the nested host becomes visible. The existing global host remains intact for other pages. No toast is rendered as an inline graph message.

### 2. Reusable multi-select dropdown

New shared component:

- `Components/Common/Selection/AppDropdown.razor`
- `Components/Common/Selection/AppDropdown.razor.css`
- `Components/Common/Selection/AppDropdownOption.cs`

Features: single or multiple selection, dynamic options, search, checkbox list, select all, clear, confirm, click-outside dismissal, ESC dismissal, aria roles and disabled mode. Selection is controlled through `Value/ValueChanged` or `SelectedValues/SelectedValuesChanged`, allowing other pages to reuse it without database dependencies.

`DatabaseInspectorPage` uses the component twice: single-select external connection and multi-select **جدول متمرکز**. Multiple selected tables appear as independent root vertices (`Level=0`). Removing a selected table prunes expansions that are no longer reachable from any remaining selected root. Expansions that remain connected to a selected root are preserved. Database metadata stays server-side with the existing permission checks.

### 3. Repainted zoom instead of transformed HTML scaling

`wwwroot/js/database-inspector-graph.js` now separates translation into a `.dbi-pan` wrapper and uses CSS `zoom` on the underlying graph content. This instructs the browser to re-layout/repaint HTML table cards and their text instead of stretching a cached transformed HTML layer. SVG paths remain vector. Mouse-wheel anchor mathematics, dragging, and fullscreen are retained. Initial camera fit now considers multiple selected roots. Source Explorer's separate SVG renderer was not modified.

### 4. Skyline tool shortcut

The Tools dropdown has a new **بررسی دیتابیس** entry, with `data-tool-shortcut="i"` and its existing access gate. Keyboard behavior matches the other items: open Tools with **Ctrl+I**, then press **I**. The server-side click handler verifies `AccessKeys.DatabaseInspector` again before navigating to `/tools/database-inspector`.

### 5. Page layout

Removed the entire custom header row `TOOLS / DATABASE INSPECTOR`, `Database Inspector`, description, and `READ-ONLY SCHEMA`. Existing CENOPS global breadcrumbs remain. The page continues to fill the workspace without a page scrollbar; long SSMS-style table detail panels scroll internally.

## Deep test database

File: `tools/database-inspector/CENOPS_GraphLab_Complex_Schema.sql` (also distributed independently as `CENOPS_GraphLab_Deep_Trade_Schema_2_49.sql`).

Creates **`CENOPS_GraphLab_Deep`**, a *new* standalone SQL Server database so your earlier `CENOPS_GraphLab` is not overwritten. Requires SQL Server 2019+ and CREATE DATABASE/table rights. Safe one-time creation: refuses to overwrite an existing populated lab schema, and never touches the CENOPS `Default` connection.

- **43 tables / eight schemas / 136 declared FK constraints**.
- An explicitly checked **22-hop chain** through tenancy, procurement, shipment, inspection, correction, assessment, payment, release and auditing.
- Multiple references to one parent; composite foreign keys; self-references; shared ancestors; cycles (`ImportPermit ↔ ComplianceDocument`, `Shipment ↔ DeliveryAppointment`); references across schemas.
- No business data is inserted. SQL Server's `sys.*` metadata suffices for graph tests.

**Run in SSMS**, then add an explicitly permitted external SQL Server connection pointing to `CENOPS_GraphLab_Deep` under your normal `ConnectionStrings` configuration. Do not point `Default` at the lab.

### A challenging manual test

1. Launch `/tools/database-inspector` or open Tools with `Ctrl+I` and press `I`.
2. Select the new external database and choose **`core.Tenant`**, **`shipping.Shipment`**, and **`finance.DutyAssessment`** together. Verify three root table cards appear.
3. From `core.Tenant`, expand **PK** through `party.Company`, `trade.PurchaseOrder`, `trade.OrderLine`, `trade.ShipmentBooking`, `shipping.Shipment`, `shipping.Container`, `shipping.ContainerLine`, `customs.Declaration`, `customs.DeclarationLine`, `customs.Inspection`, `customs.InspectionFinding`, `customs.CorrectionCase`, `customs.CorrectionTask`, `finance.DutyAssessment`, `finance.PaymentInstruction`, `finance.PaymentTransaction`, `finance.BankReceipt`, `release.ReleasePermit`, `release.GatePass`, `release.GateEvent`, `audit.EventJournal` and `audit.ArchiveItem`.
4. Reverse the chain with **FK**: selected parent tables must appear to the **left** of the child. For tables with multiple declared constraints, verify the checkbox menu.
5. Test a composite FK on `trade.OrderLine` → `trade.PurchaseOrder` (TenantId/OrderNumber) and on `shipping.ContainerLine` → `shipping.Container` (ShipmentId/ContainerCode). Each composite constraint must render as one relationship.
6. Verify self-reference and cycles (e.g., `core.Region`, `trade.ImportPermit`, `trade.ComplianceDocument`). They must not duplicate an existing vertex.
7. Select and remove tables in the multi-select picker. Verify orphan branches disappear while connected branches remain; select all and clear.
8. Select a table with no outgoing FK (e.g., `core.Tenant`) and click **FK**. Check the **bottom-left toast** in both normal mode and fullscreen.
9. Zoom in via graph toolbar and Ctrl+wheel. Text should remain sharp; drag and reset should work.

## Verification

- JS syntax via Node `--check`: passed.
- CSS parse via `tinycss2`: passed.
- XML project files: passed parsing.
- SQL fixture: mechanically verified 43 distinct `CREATE TABLE` declarations, 136 distinct `FOREIGN KEY` declarations, and the 22-hop backbone.
- Chromium smoke test via Playwright: multiple root positions, edges, PK action callbacks, repaint-based zoom, graph updates, fullscreen notification visibility — passed.
- `.NET`/Razor build and real SQL Server execution: **not run** in this environment (no .NET SDK / SQL Server instance). Run `dotnet build` locally and execute the SQL script in a dedicated test SQL Server.
- Optional browser fixture and harness: `tests/DatabaseInspectorGraphBrowser/fixture.html`, `test_graph.py` (requires Playwright and Chromium).

## Deployment

No CENOPS EF migration and no Source Intelligence reindex are needed. Apply changes to the codebase, rebuild, and reload the browser to invalidate cached JS. Access permission from release 2.47 must already exist.
