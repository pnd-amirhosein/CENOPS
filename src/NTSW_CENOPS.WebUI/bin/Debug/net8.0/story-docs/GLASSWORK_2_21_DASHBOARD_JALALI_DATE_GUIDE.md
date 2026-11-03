# Glasswork 2.21 — Dashboard Stability, Jalali Date UI & User Birthdate

**Base:** Glasswork 2.20. **Database:** one additive EF Core migration. **Framework:** .NET 8 / Blazor / EF Core / ASP.NET Identity.

## Changes

### 1. Dashboard dropdown crash

`Home.LayoutFor(...)` previously used `First(...)` to fetch a geometry reference for a rendered card. Switching dashboards changed `_personalRefs` before `_personalTiles` finished resolving. An intermediate render could request a card still belonging to the previous dashboard, causing `InvalidOperationException: Sequence contains no matching element`.

- `LayoutFor(...)` now has a defensive fallback (no unhandled exception on transient references).
- `OnSelectionChanged(...)` resolves the next dashboard's cards into **temporary variables**, then publishes the references, cards, ID and name together.
- `_selectionVersion` rejects late responses from older dropdown changes. `LoadAsync` invalidates outstanding dropdown requests.
- Existing layout JSON and saved card geometry are unchanged.

### 2. Report cards and narrow chart readability

- Dashboard report tiles use **only their built-in expand control**, not the generic preview/eye action, even when chart data fails to load.
- The report SVG measures the chart plot's *physical* width/height instead of relying only on a static viewBox width.
- X-axis tick density is based on **available physical pixels** and tick label length; narrow cards display fewer tick labels rather than overlapping dates.
- Date tick labels and hover details use Jalali display text, without changing `AxisValue`, underlying dataset timestamps, or date filtering.
- Explicit SVG tick font sizing is adjusted for short/tall plot containers (including line, step, smooth and area variants).
- Existing chart zoom, report expansion and date range semantics are retained.

### 3. Reusable Jalali conversion and datepicker

New common files:

- `src/HealthMonitoringApp.WebUI/Components/Common/Dates/JalaliDate.cs`
- `src/HealthMonitoringApp.WebUI/Components/Common/Dates/AppJalaliDatePicker.razor`
- `src/HealthMonitoringApp.WebUI/Components/Common/Dates/AppJalaliDatePicker.razor.css`

`JalaliDate` is a **presentation-only helper** based on `System.Globalization.PersianCalendar`:

- `JalaliDate.Date(DateTime | DateOnly | DateTimeOffset)` — Jalali `yyyy/MM/dd` display.
- `JalaliDate.DateTime(DateTime | DateTimeOffset)` — Jalali display plus the existing time.
- `JalaliDate.AxisLabel(string)` — converts only recognized Gregorian report timestamps for presentation.
- `JalaliDate.Gregorian(jalaliYear, jalaliMonth, jalaliDay)` — produces a native Gregorian `System.DateTime`.

`AppJalaliDatePicker` accepts `DateTime? Value` and emits `EventCallback<DateTime?> ValueChanged`. Month and year are chosen in the Persian calendar, with month navigation, bounds, a clear action, an optional Today action, outside-click dismissal and Escape support. The supplied value is **never a Jalali date string**.

Example:

```razor
@using HealthMonitoringApp.Components.Common.Dates
<AppJalaliDatePicker @bind-Value="_birthDate" Max="DateTime.Today" />
@code { private DateTime? _birthDate; }
```

For a native HTML POST, use a hidden ISO **Gregorian** form value:

```razor
<input type="hidden" name="birthDate"
       value="@(_birthDate?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) ?? "")" />
```

### 4. Existing UI date fields

Converted display paths include:

- Chat conversation timestamps (date portion only; time-only displays remain unchanged).
- Automated-job history and last run timestamps.
- Report lists and report dataset inspection.
- Weather page date display.
- Report chart axes, hover content and native date-range controls.
- Generic `DataTable<T>` read-only/view-date rendering and editable `DateTime` / `DateOnly` fields.
- Generic DataTable date filter inputs, emitting Gregorian ISO `yyyy-MM-dd` filter strings to preserve the existing filter API.

**Important:** All service, persistence and domain values remain native .NET Gregorian `DateTime`, `DateTimeOffset` or `DateOnly`; only strings rendered for people are Jalali.

### 5. User birthdate

- Added `ApplicationUser.BirthDate` as nullable `DateTime?`, stored in SQL Server as `date`.
- Added the field to the existing **ویرایش اطلاعات کاربری** modal using `AppJalaliDatePicker`.
- The existing `/auth/profile` POST receives `yyyy-MM-dd`, validates the Gregorian value, and forwards it through the application service and identity gateway.
- A null/empty value clears the optional date. The existing ASP.NET Identity profile update / cookie refresh flow is preserved.
- The existing saved DOB is loaded from the current authenticated account before opening the modal.
- Birthdates are not placed into redirect query strings on validation errors.
- Birthdates in the future (or unreasonable historical years) fail validation.

## EF Core migration

Added:

`src/HealthMonitoringApp.Infrastructure/Migrations/20261015160000_AddUserBirthDate.cs`

This adds `AspNetUsers.BirthDate date NULL` with no backfill and updates `AppDbContextModelSnapshot`. Existing users remain valid with a null birthdate. Previous shared-dashboard migrations are preserved.

From the solution root, after backing up your database:

```bash
dotnet build
dotnet ef database update --project src/HealthMonitoringApp.Infrastructure --startup-project src/HealthMonitoringApp.WebUI
```

Only apply the migration to the correct target database. It is additive and does not require a dashboard-layout conversion.

## Local verification checklist

1. Log in; switch rapidly between dashboards whose cards differ. No `LayoutFor` exception, stale response or card mix-up should occur. Also test switching to an empty dashboard and back.
2. Test report card expand without a generic eye; deliberately make a report card narrow and short. Dates must not overlap; Y-axis/X-axis text must remain legible. Test width changes while editing and after Save/reload.
3. In the report explorer choose custom date range using the Jalali picker; verify dates look Jalali but returned report data/filter logic is identical to Gregorian ranges.
4. Check Persian date display in chat, reports, scheduled jobs, history, generic table dates and weather.
5. Open profile edit, set a birthdate in another Jalali year/month, save, reopen, and verify the same Jalali date. Check SQL value for the corresponding Gregorian date. Clear the date and save again.
6. Confirm future birthdates cannot be picked, and malformed/tampered Gregorian form values are rejected.
7. Verify the migration works against an existing database with user records and that authentication/profile edits remain functional.
8. Test Jalali datepicker inside both modals and table filters: outside click/Escape closes only the calendar, not the parent modal.

## Verification performed in this workspace

- Targeted source-flow assertions, archive integrity checks, CSS parsing and XML parsing.
- JavaScript syntax checking for `site.js` and `monitor-chart-interactions.js`.
- JavaScript function checks for dashboard chart measurements in narrow/wide simulated containers.

**Not performed:** actual .NET SDK build, EF migration execution, Blazor renderer/browser integration, or SQL Server integration. These require a suitable local environment.
