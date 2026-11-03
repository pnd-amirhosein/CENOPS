# Glasswork 2.10 — Daily job data storage

This release adds **only** the storage entity, service contracts, EF Core mapping,
migration, and dependency-injection registration. No UI, background-worker,
SQL-executor, chart, or job-history behavior was changed.

## Entity: `AutomatedJobData`

- `Id` (`bigint` identity, PK)
- `AutomatedJobId` (`bigint`, FK to `AutomatedJobs`)
- `Date` (`date`, chosen by the caller)
- `Value` (`nvarchar(max)`, JSON array of windows; defaults to `[]`)
- `LastCalledAtUtc` (`datetimeoffset`, updated after a successful save)
- `RowVersion` (`rowversion`, SQL Server optimistic concurrency token)
- Unique index on `(AutomatedJobId, Date)`.

## Service

Inject `IAutomatedJobDataService` from `HealthMonitoringApp.Application.Jobs`.

- `GetDayAsync(jobId, date)` returns the complete daily document.
- `ListDaysAsync(jobId, from, through, take)` returns only day metadata (no JSON).
- `UpsertWindowAsync(jobId, date, windowStartUtc, windowEndUtc, rowsJson)`
  accepts a JSON **array**, creates the day if needed, appends a window, or
  replaces an existing window with the same start/end timestamps.
- `DeleteDayAsync(jobId, date)` deletes a daily document.

Example of `Value`:

```json
[
  {
    "windowStartUtc": "2026-10-12T00:00:00+00:00",
    "windowEndUtc": "2026-10-12T06:00:00+00:00",
    "collectedAtUtc": "2026-10-12T06:00:04+00:00",
    "rows": [{ "hour": "2026-10-12T05:00:00Z", "success": 120 }]
  }
]
```

**Day boundaries:** `Date` is an explicit argument, not derived from server
local time. The eventual caller decides which calendar day a window belongs to.
This avoids silently misplacing windows around midnight or time-zone changes.

**Concurrency:** SQL Server rowversion prevents lost updates, and the unique
index prevents duplicate day records. Concurrent insert/update collisions are
retried with a fresh DbContext up to five times.

**Bounds:** Each input `rowsJson` is limited to 1,000,000 characters, and each
daily document to 4,000,000 characters. Large datasets may need a different
storage model; this release intentionally targets compact monitoring results.

**Permissions:** The storage service is an internal persistence API and does
not authorize end users. When a future UI or API exposes it, enforce job-view
and job-management permissions in that entry point.

**Important:** This version does **not** automatically store results from
`AutomatedJobRunner`. That integration will be designed when requested.

## Migration

Apply the new migration after Glasswork 2.9 / 2.9.1 migrations:

```bash
dotnet ef database update --project src/HealthMonitoringApp.Infrastructure --startup-project src/HealthMonitoringApp.WebUI
```

Back up the CENOPS database before applying. This migration creates one table
and one unique index; it does not change existing jobs or job history.

## Validation

Source and archive checks performed; .NET SDK and SQL Server are unavailable
in the packaging environment, so compilation and migration execution must
be tested locally.
