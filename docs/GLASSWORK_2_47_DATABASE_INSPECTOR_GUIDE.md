# Glasswork 2.47 — Database Inspector

## Scope
Standalone, metadata-only SQL Server inspector. **Source Intelligence (2.46.1) is unchanged.**

Menu: `ابزارها → Database Inspector` · Route: `/tools/database-inspector`.

## Start-up and interactions

1. Open the tool as an authenticated user with `tools.database-inspector` (granted to all existing active roles in migration). The dropdown probes **external named** SQL Server connections from `ConnectionStrings` using a short timeout, omits `Default`, omits matching `Default` database names on the same configured server, and omits inaccessible/empty/metadata-invisible databases. **No connection string or password is returned to the browser.**
2. Pick an external connection, then one of its visible `schema.table` names. The selected table is the focused node.
3. **PK →** finds every declared foreign-key relationship **whose referenced table is this node**, and adds those child tables one lane to its right. PK here means *incoming references to the table*, including foreign keys to eligible unique keys, not exclusively the table's primary-key columns.
4. **← FK** finds declared foreign keys **owned by this node**, then adds referenced parent tables one lane to its left. One FK **constraint** expands immediately. Multiple independent FK constraints show an anchored checkbox menu with `انتخاب همه`, and only checked relations are added. One composite FK with several columns is **one selectable relationship** (not multiple parents).
5. Click the table card (not its PK/FK buttons) to open the **top-right SSMS-inspired table designer**. Tabs: Columns (SQL types, nullability, PK/FK flags, identity, computed/default/collation properties), Foreign keys (column mapping; update/delete actions), Indexes. Hover never exposes table data/tooltip.
6. Drag vertices, pan empty space, use Ctrl + wheel to zoom, or the toolbar for zoom/reset/fullscreen. The details panel and FK menu remain inside the fullscreen graph.

### Permission migration (required)

`20261026140000_AddDatabaseInspectorAccess` adds the system permission and assigns it to all existing **active** roles. The default role seeder grants it to subsequently created `User`, `Admin`, and `Owner` roles. Custom roles created later must be granted the permission through Access Management. Existing role grants remain editable by the owner.

Run the normal application migration process (e.g. `dotnet ef database update --project src/HealthMonitoringApp.Infrastructure --startup-project src/HealthMonitoringApp.WebUI`). The migration **does not query or mutate external databases**.

### Optional explicit source allowlist

By default all *named external* entries under `ConnectionStrings` are considered. Operators may restrict availability by adding:

```json
"DatabaseInspector": { "ConnectionNames": ["SiLog", "SrLog", "UiLog"] }
```

This is an optional configuration example: no configuration file needs changing if auto-discovery is desired. `Default` and same server/catalog as `Default` are rejected even if listed. Treat runtime secrets as confidential; use User Secrets / environment secrets for production.

### Metadata permissions

The SQL login needs enough SQL Server **catalog metadata visibility** (`sys.tables`, `sys.columns`, `sys.foreign_keys`, `sys.foreign_key_columns`, `sys.indexes`, etc.). Grant `VIEW DEFINITION` on the target database if your security policy allows it, or appropriate object metadata visibility. The inspector issues catalog `SELECT` queries only; it never reads user rows or runs DML/DDL. Network/server access and metadata permissions are enforced by SQL Server itself.

### Important boundaries

- Relationships are based strictly on **declared SQL foreign keys**, not similarly named columns. No inferred ER edges.
- A table is keyed by database-local `object_id` and has at most one graph vertex. Shared parents and cycles connect existing nodes; cycles do not recursively auto-expand. With an existing shared vertex, an edge can cross lanes rather than duplicating the vertex.
- At most 400 table vertices in the interactive diagram to prevent overload; the UI states if it omits further vertices. The connection and table selectors always contain only visible metadata.
- If SQL Server has no table metadata visible to a connection, it won't be shown as a selectable source. No local SQL Server instance or real schema was available for server-side integration testing in the delivery environment.

## Suggested acceptance run

1. Apply migration. Login as ordinary User (and Admin/Owner), check menu and direct route.
2. Select a database with recognized foreign keys; `Default` must not appear, nor an unreachable external database.
3. Choose `Orders`; PK expands `OrderItems` right and FK expands `Customers` left. Verify exact FK names in SSMS.
4. Pick a table with several independent FK constraints; test select-all, selecting one, Cancel, and repeated expansion.
5. Test a **composite FK** (one selectable relation regardless of column count), self-referencing table, and shared parent.
6. Click cards to inspect columns/indexes/FKs; move nodes and toggle fullscreen.
7. Verify no read of application table rows, no raw connection string in browser HTML/JS or error messages.

Optional **isolated database** example: `tests/DatabaseInspectorSqlFixture/SchemaFixture.sql` creates a fixture in a dedicated `di_test` schema. Run only against an expendable dedicated test database; it is not executed by CENOPS.

### Verification status

Client JS syntax and Chromium diagram smoke tests passed; CSS and project XML were parsed; source and package integrity checked. **.NET compilation, Razor compilation, EF migration execution, and a real SQL Server metadata query were not possible without a .NET SDK and reachable test SQL instance.** Please run `dotnet build` and test against an actual authorized external SQL Server before production deployment.
