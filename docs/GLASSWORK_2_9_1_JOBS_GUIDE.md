# Glasswork 2.9.1 — Automated Jobs refinements

Built as a minimal upgrade to 2.9. This release **does not change the EF model or require another migration**. The existing `AutomatedJobs` and `AutomatedJobRuns` entities/tables remain in use.

## Settings / DataTable

- `/settings` → **کارهای خودکار** shows the configured source's **SQL Server / database name**, not a fixed `SiLog` / `SrLog` / `UiLog` display label.
- The source dropdown uses stable connection *keys* internally. The names visible to users come from parsed `ConnectionStrings` in `appsettings.json`.
- After create, edit, and delete, the existing DataTable handlers re-fetch from SQL. Parent page state is invalidated after the load so the new rows and statuses actually appear. The same refresh behavior is applied to the Access table, while Users and Roles already invalidate on fetch.
- Row ellipsis → **مقدار SQL کار** still views/edits SQL with the snippet component.
- Row ellipsis → **تاریخچه اجرا** opens a **dedicated read-only modal**, showing up to 75 latest records as compact date, trigger (scheduled/manual), and status rows.
- Row ellipsis → **اجرای آزمایشی** opens the independent test modal. After the run, the job table is re-fetched. The execution is also visible in History.
- Empty SQL cannot be enabled or tested. The runner rejects such a job **before inserting a run record**. Clearing a query disables its job.

## Configuring monitored database connections

`appsettings.json` now includes named connection strings `SiLog`, `SrLog`, and `UiLog`. In this example they are copies of `ConnectionStrings:Default` with **only `Database` changed**, as requested.

```json
{
  "ConnectionStrings": {
    "Default": "Server=...;Database=HealthMonitorDb;...",
    "SiLog": "Server=...;Database=SiLog;...",
    "SrLog": "Server=...;Database=SrLog;...",
    "UiLog": "Server=...;Database=UiLog;..."
  },
  "Jobs": {
    "SourceConnectionNames": ["SiLog", "SrLog", "UiLog"],
    "TimeZoneId": "Asia/Tehran"
  }
}
```

`Jobs:SourceConnectionNames` is the **explicit job-source allowlist**; simply adding a new application connection string does not grant execution access. To add a new permitted monitoring source, add its named `ConnectionStrings` entry and then its key to `Jobs:SourceConnectionNames`. The UI updates automatically without source-specific code. The database named `Default` and any source pointing to the same server/database as `Default` are excluded.

For deployed environments, **override these three connection strings using environment variables or a secrets manager**, for example `ConnectionStrings__SiLog`. The existing `Jobs__Sources__*` variables from 2.9 are **superseded by the named connection strings** in 2.9.1.

### Critical production security note

The example appsettings copies the credentials from your `Default` connection **because that was explicitly requested**. If those credentials are a SQL `sa`/write-capable account, this is **not safe for production**. Before deploying scheduled queries to production, override the three monitoring connections with dedicated `SELECT`-only accounts. `ApplicationIntent=ReadOnly` **does not** enforce SQL Server permissions on a normal SQL Server primary. The syntactic SELECT check is not a security substitute for read-only SQL logins.

## Existing job execution-history schema

The previously introduced `AutomatedJobRuns` table already records a run's job ID, start/end timestamps, manual/scheduled trigger, and status. There is **no need for a second run-history table or an EF migration**. This update makes the history interface intentionally concise and avoids fetching or showing large result previews or errors in the history list.

## Quick tests

1. Create a disabled job; confirm the new row is visible immediately.
2. Try to enable it with no SQL: expect a failure and an editable draft, not a scheduled job.
3. Set a valid SELECT query via the SQL modal and then enable it; verify the updated table renders without manual refresh.
4. Check server/database strings shown in the dropdown and table; changing appsettings source names should change their labels.
5. Test the job; verify one run in **تاریخچه اجرا** with correct manual status and date.
6. Trigger an actual scheduled slot; verify a scheduled status is recorded.
7. Remove SQL and confirm the job is automatically disabled; testing with no SQL should be rejected before run logging.
8. Edit/delete Users, Roles, Access, and Jobs in Settings; rows should update without a manual refresh.
9. Remove one allowed monitoring connection string and restart: its source must not be selectable or executable. The runner must **never fall back** to `Default`.
10. Log in without `settings.jobs.view/manage/execute` and verify the same server-side permission restrictions remain active.

**Validation:** Source, JSON, CSS and package checks were performed in the artifact environment. A .NET build, real SQL Server connection, migration, scheduled worker test, and browser tests must be carried out in your deployment environment.
