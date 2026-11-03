# Glasswork 2.11 — Monitoring reports: management and creation wizard

Built on **Glasswork 2.10.1**. No change to the automated job scheduler, SQL policy, or daily JSON storage.

## Pages

- `/reports` — list, search, edit, delete report definitions; empty state and deletion confirmation.
- `/reports/new` — create report wizard.
- `/reports/edit/{id}` — edit an existing report using the same wizard.
- Both list and create pages are linked under **سرویس مانیتورینگ**.

## User flow

1. Enter report name and select an existing job from the dropdown (including disabled jobs).
2. Service reads the **most recent calendar-day row** in `AutomatedJobData` for the job, then selects the **latest execution window** inside its JSON `Value`.
3. If there is no row or the newest window contains no rows, show a clear no-data message. Running SQL is **never** triggered by the wizard.
4. If there is data, validate each row of the latest window. Each row must have consistent properties. Exactly one comparison field must be selectable (datetime, or numeric); every other field must be numeric. A datetime column, when present, must be the comparison field. Null measurements are accepted; missing properties and inconsistent types are rejected.
5. Select comparison field, chart type (line/bar/scatter), and save. All other numeric columns become measures; users don't need to know their names in advance.
6. When saving, the backend **re-inspects** the latest window and rebuilds the saved measures list so the UI cannot bypass validation.

## Database model

`MonitoringReports`: `Id`, `Name`, `AutomatedJobId`, `ComparisonField`, `ChartType`, `MeasureFieldsJson`, `CreatedByUserId`, `CreatedAtUtc`, `UpdatedAtUtc`.

The report stores **configuration**, not duplicated monitoring data. There is a cascade relation to `AutomatedJobs` and a restrictive creator FK to users. It does not currently render full historical charts; that's a separate feature after report definitions are available.

Migration: `20261012193000_AddMonitoringReports` (also inserts permissions).

Apply from solution root, after backing up the application database:

```bash
dotnet ef database update --project src/HealthMonitoringApp.Infrastructure --startup-project src/HealthMonitoringApp.WebUI
```

## Permissions

- `reports.view` — list reports, inspect job schema.
- `reports.manage` — create/edit/delete report definitions.

Both permissions are assigned on migration to built-in **User, Admin, Owner** roles so general users can create reports as requested. Admins can revise assignments via the existing role access management system. Access control is checked at page and service levels.

## Technical notes

- A job is usable with `IsEnabled=false` **if it has stored data**.
- Uses newest daily record, **not a union of historical rows**; oldest data and earlier windows aren't fallback when the newest is empty.
- Only flat JSON objects with numeric/date values qualify. String dimensions, multiple datetime columns, booleans, nested objects/arrays, inconsistent/missing fields are rejected. Reporting on text categories requires extending the contract later.
- The report editor does **not** execute live production queries; it reads stored data only.
- The latest window type contract is validated now; historical aggregation and report rendering aren't implemented by this release.
- Database data and SQL connection strings aren't shipped in the archives.

## Local test checklist

1. Apply migrations, sign in as a user with reports permissions and open `/reports`.
2. Verify empty state, then use `گزارش جدید`.
3. Select a job never run: confirm no-data message and disabled save.
4. Select an inactive job with saved data: confirm analysis is available.
5. Use test data resembling `{ "Hour":"2026-10-12T13:00:00", "TotalCount":208, "SuccessCount":168 }`: expect comparison dropdown `Hour`, two measures, and enabled save after providing name.
6. Select all-numeric data: each number can be chosen as the comparison, with other numbers as measures.
7. Select a dataset with `ServiceName:"foo"`: verify invalid schema and disabled save.
8. Confirm create, list, search, edit, and delete. Deleting a report must not delete stored job data.
9. Remove job access (`reports.view`, `reports.manage`) from role: confirm both page and service authorization.
10. Modify a job's latest stored output during editing: saving must revalidate and reject stale field selections.

## Validation scope

The solution was checked structurally and archives were verified. A .NET SDK and SQL Server are unavailable in the build environment, so compilation and database migration execution must be run in the target developer environment.
