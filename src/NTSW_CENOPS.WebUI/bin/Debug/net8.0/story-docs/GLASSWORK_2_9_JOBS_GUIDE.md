# Glasswork 2.9 — Automated Jobs (Step 2)

## Overview

Automated Jobs are **executable monitoring configurations**, separate from the non-executable SQL Knowledge Library. This version adds:

- `AutomatedJobs`: name, interval (minutes), source (`SrLog`, `SiLog`, `UiLog`), author Identity ID, stored SQL, enabled status, created/updated timestamps, last run and result.
- `AutomatedJobRuns`: manual/scheduled, planned timestamp, start/end, success/failure, row count, safe numeric-only result preview, diagnostic error.
- A persistent .NET `BackgroundService` polling on a 15-second timer.
- DataTable management in **Settings → کارهای خودکار**, SQL code modal, manual Test, recent run history.
- Separate RBAC permissions: `settings.jobs.view`, `.manage`, `.execute` (initially Admin/Owner only; role assignments remain editable in Access management).

## 1. Apply EF Core migration

Back up the CENOPS database. After checking your migration history against your current source, run:

```powershell
dotnet ef database update --project src/HealthMonitoringApp.Infrastructure --startup-project src/HealthMonitoringApp.WebUI
```

Migration: `20261012150000_AddAutomatedJobs`.

**Never run this migration against SrLog, SiLog, or UiLog.** These databases are strictly query targets; all CENOPS job data is stored in the `Default` database.

## 2. Configure three separate READ-ONLY source credentials

Configure keys using environment variables, deployment secrets, or local User Secrets. **There are no log-database passwords in this archive.** Examples of environment key names:

```
Jobs__Sources__SrLog
Jobs__Sources__SiLog
Jobs__Sources__UiLog
Jobs__TimeZoneId=Asia/Tehran
Jobs__CommandTimeoutSeconds=20
Jobs__MaxRows=200
Jobs__MaxConcurrent=3
```

`Jobs:Sources:SrLog` must point to **SrLog**, etc., not the CENOPS database. Example of a *placeholder* source string:

```
Server=<sql-host>;Database=SrLog;User Id=<read-only-login>;Password=<secret>;Encrypt=True;TrustServerCertificate=False
```

Use dedicated SQL login(s) whose only granted data access is `SELECT` on the required log tables. `ApplicationIntent=ReadOnly` is sent with the connection but **does not grant or enforce read-only permissions** on a normal primary SQL Server connection.

If a source connection is missing, jobs targeting it will fail and appear as Failed in history. No fallback to the CENOPS database occurs.

The scheduling time zone defaults to `Asia/Tehran`. Configure a different valid IANA/Windows time-zone ID if your deployment uses another business time zone. Timestamps are stored in UTC; the calculation uses local wall-clock time.

## 3. Midnight-anchored iteration

`IterationMinutes` has allowed range 1–1440. It is **not** calculated as `last run + iteration`.

- `45` → 00:45, 01:30, 02:15, 03:00, …, 23:15 (next day's first run is 00:45)
- `60` → 01:00, 02:00, …, 23:00 (next day's first run is 01:00)
- `1440` → 00:00 once per day.

When the process is stopped over a slot, the service does **not** backlog/replay historical slots; only slots at most 90 seconds old are eligible for execution. Jobs enabled mid-day wait until their next due slot. Every scheduled run has a unique `(JobId, ScheduledAtUtc)` key to protect against duplicate execution claims. In-process manual/scheduled execution for one job is serialized. For multiple application instances, use a single scheduler instance or add a distributed execution lease in a later step. Scheduled jobs execute with configurable bounded concurrency (3 by default, maximum 8); one individual job can never overlap itself in-process.

## 4. Use the settings page

1. Navigate to `/settings` and open **کارهای خودکار**.
2. Click `جدید`. Enter a name, `IterationMinutes`, `SourceKey` (`SrLog` / `SiLog` / `UiLog`), and leave **فعال** off until the SQL is defined.
3. Save the job. Its author is the authenticated user.
4. Open the row's ellipsis → **مقدار SQL کار**. Choose `افزودن دستور`, enter SQL in the snippet editor, and confirm save.
5. Select the job and edit **فعال** to enable scheduled execution.
6. Row ellipsis → **اجرای آزمایشی** to run now and inspect the latest execution records. Manual runs never move the scheduled boundary.
7. Refresh the DataTable to see scheduled statuses.

`IterationMinutes` and `SourceKey` are in-table text/number fields (with service-side validation). SQL is edited **only through the snippet modal**. The author cannot be changed by editing a row.

### Example monitoring SQL — two-hour method health

For **SrLog**, where the method is `MethodName`:

```sql
SELECT MethodName AS ServiceMethodId,
       COUNT_BIG(*) AS Total,
       SUM(CASE WHEN ErrorCode = 0 THEN 1 ELSE 0 END) AS SuccessCount,
       SUM(CASE WHEN ErrorCode <> 0 AND Exception IS NULL THEN 1 ELSE 0 END) AS BusinessErrorCount,
       SUM(CASE WHEN ErrorCode <> 0 AND Exception IS NOT NULL THEN 1 ELSE 0 END) AS SystemErrorCount
FROM dbo.Response
WHERE ResponseTime >= DATEADD(HOUR, -2, SYSDATETIME())
GROUP BY MethodName
```

For **SiLog**, use `ServiceMethodId`, and `dbo.ResponseSiLog`. For **UiLog**, use `ServiceMethodId` and `dbo.ResponseUiLog`.

Results are **not yet aggregated into a reporting warehouse** (that's a future phase). This release saves execution metadata and a small, numeric-only result preview, intentionally avoiding copying input/output/exception/user payloads into CENOPS.

## 5. Access and execution safety

- View permission: `settings.jobs.view`.
- Add/edit/remove job or SQL: `settings.jobs.manage`.
- Execute test: `settings.jobs.execute`.
- Scheduled worker executes *approved enabled configurations* in the background using source SQL credentials, independent of an interactive user's session.
- Server-side RBAC is checked on every action. Hiding a button is not the security boundary.
- SQL policy accepts one `SELECT` query and rejects comments, semicolons, and SQL mutation/administration keywords. **This simple syntactic restriction is only defense-in-depth**; mandatory read-only source credentials are the real boundary.
- 20-second default command timeout, 200-row read cap, 5-row numeric-only preview, and error logging.
- Job run errors may contain SQL Server metadata, so keep the settings-page permission restricted to trusted admins.

## 6. Verification checklist

- Apply the migration to a disposable CENOPS database.
- Configure all 3 test-source SQL logins and confirm SQL permissions restrict writes.
- Create disabled job; SQL modal displays the empty-state message.
- Save a SELECT query; enable job; use Test Run; verify success and execution history.
- Test an invalid query, missing connection, and a read-only credential denied access; verify failures are visible and non-destructive.
- Verify UTC+business-time-zone boundary behavior for 45 and 60 minutes, including crossing midnight.
- Log in as a User: no job tab by default, and no server-side management/test privileges.
- Log in as Admin: tab visible with management and test functions.
- Reassign role permissions to prove server-side permission checks use current grants.
- Test 2 job attempts overlapping (one job must not run twice simultaneously).

**Validation limitation:** this artifact was constructed and statically inspected without the .NET SDK or a running SQL Server in the artifact environment. Build, migration, browser, and database integration tests must be run locally before production deployment.
