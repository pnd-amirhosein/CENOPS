# Glasswork 2.10.1 — Automatic job-result persistence

Base: Glasswork 2.10. **No new database migration** (uses `AutomatedJobData` from `20261012180000_AddAutomatedJobData`).

## Changes

- `Administration.Jobs.cs`: creates `_jobColumns` inside `OnInitialized()` so the `SourceLabel` instance delegate is valid.
- `JobQueryExecutor.cs`: materializes the **actual SELECT rows** into a JSON array, while continuing to return a short privacy-conscious numeric-only preview for the existing history field.
- `AutomatedJobRunner.cs`: after SQL succeeds, calls `IAutomatedJobDataService.UpsertWindowAsync` with the result array **before** declaring the execution successful. Both scheduled and manual Test runs use this path. SQL/storage failure records a failed run without inserting an empty success result. Shutdown cancellations record a cancelled run.
- `JobSchedule.cs`: ensures a 00:00 boundary can close the previous day for intervals that divide 24 hours (e.g. a six-hour job records the 18:00–00:00 window).

## Storage layout

`AutomatedJobData` contains one row for each `(AutomatedJobId, Date)` combination. `Value` is an array of window envelopes:

```json
[
  {
    "windowStartUtc": "2026-10-12T02:30:00+00:00",
    "windowEndUtc": "2026-10-12T08:30:00+00:00",
    "collectedAtUtc": "2026-10-12T08:30:04+00:00",
    "rows": [
      { "Hour": "2026-10-12T10:00:00", "TotalCount": 800, "SuccessCount": 710 }
    ]
  }
]
```

The `Date` is the date of the **start of the collection window in `Jobs:TimeZoneId`** (the default is `Asia/Tehran`). This ensures that a 00:00 run closing yesterday's data updates yesterday's document. A retry of an *identical start/end window* replaces that envelope instead of duplicating it. Concurrency is protected using the existing unique index and SQL Server rowversion.

- Scheduled execution: window end = scheduled slot; window start = end minus `IterationMinutes`.
- Manual Test: window end = manual execution start; window start = end minus `IterationMinutes`. Separate manual tests use distinct windows and are stored as distinct envelopes.
- Zero-row successful queries save `rows: []` as a valid result.
- The existing `AutomatedJobRuns` entity remains the brief execution log and does **not** contain the full result JSON.

## Parameterized time windows

The query runner now provides four optional SQL parameters:

| Parameter | Type | Meaning |
|---|---|---|
| `@WindowStart` | `datetime2` | Window start in configured *local job timezone* |
| `@WindowEnd` | `datetime2` | Window end in configured local job timezone |
| `@WindowStartUtc` | `datetime2` | Window start in UTC |
| `@WindowEndUtc` | `datetime2` | Window end in UTC |

For example, **if `CallTime` is stored in the same local timezone as `Jobs:TimeZoneId`**, this 6-hour SrLog job returns hourly aggregates only from its assigned window:

```sql
SELECT
    DATEADD(HOUR, DATEDIFF(HOUR, 0, CallTime), 0) AS [Hour],
    COUNT_BIG(*) AS TotalCount,
    SUM(CAST(CASE WHEN ErrorCode = 0 THEN 1 ELSE 0 END AS bigint)) AS SuccessCount,
    SUM(CAST(CASE WHEN ErrorCode <> 0 AND Exception IS NULL THEN 1 ELSE 0 END AS bigint)) AS BusinessErrorCount,
    SUM(CAST(CASE WHEN ErrorCode <> 0 AND Exception IS NOT NULL THEN 1 ELSE 0 END AS bigint)) AS SystemExceptionCount
FROM dbo.Response
WHERE CallTime >= @WindowStart AND CallTime < @WindowEnd
GROUP BY DATEADD(HOUR, DATEDIFF(HOUR, 0, CallTime), 0)
```

If `CallTime` is UTC, use `@WindowStartUtc` and `@WindowEndUtc` instead.

**Important:** The runner cannot infer the real time filter in an arbitrary SQL query. The window metadata describes the scheduled interval; use the parameters in your SQL if you want the returned rows to represent exactly that interval. A query filtering on `GETDATE() - 7 days` continues to return a seven-day dataset, even if the job runs every six hours.

## Safety and limits

- Uses the existing allowlisted monitoring source and read-only SELECT policy. No fallback to the CENOPS application database.
- Maximum output rows: `Jobs:MaxRows` (default **200**, hard maximum **1000**). Exceeding the limit **fails the run** rather than silently storing incomplete rows.
- Maximum 64 columns, 64,000 characters per text cell, and 1,000,000 characters of serialized JSON per execution window; `AutomatedJobDataService` also enforces a 4,000,000-character daily-document ceiling.
- Because full SELECT output is now stored in CENOPS, avoid selecting `Input`, `Output`, tokens, passwords, `UserData` or other sensitive payloads. Prefer aggregate queries and restricted SELECT-only SQL login permissions.
- The service is already registered with dependency injection. No UI additions or schema changes in this release.

## Local verification

1. Confirm the prior migrations through Glasswork 2.10 have been applied.
2. Create a disabled job, set a suitable aggregate SELECT (e.g., above), and save it.
3. Use **Test** and confirm the job run is marked `Succeeded`.
4. In CENOPS SQL Server, inspect:

```sql
SELECT Id, AutomatedJobId, [Date], LastCalledAtUtc, [Value]
FROM dbo.AutomatedJobData
ORDER BY Id DESC
```

5. Test again: the same day's row should be updated and its JSON should contain another manual window. Check the next day's execution creates a new dated row. Schedule an enabled six-hour job if you wish to test exact-window replacements.

Build/runtime validation and SQL Server integration tests still need to be run in your development environment; no .NET SDK or live SQL Server was available to the packaging process.
