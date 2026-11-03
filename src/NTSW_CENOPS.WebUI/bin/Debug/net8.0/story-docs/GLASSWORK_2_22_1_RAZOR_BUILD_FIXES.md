# Glasswork 2.22.1 — Razor Compilation Fixes

## Base

Based exclusively on `NTSW_CENOPS_Glasswork_2_22.zip`. This is a narrowly scoped hotfix; it does not change database schema, migrations, API contracts, authorization, or event recurrence.

## Changed files

1. `src/HealthMonitoringApp.WebUI/Components/Common/Dates/AppJalaliDatePicker.razor`
   - Removed an illegal nested `@{ ... }` block inside the calendar-day `@for` body.
   - Kept day-local variables `currentDay` and `date` as ordinary C# statements inside the existing Razor code block.
   - Addresses `RZ1010` near line 34.

2. `src/HealthMonitoringApp.WebUI/Components/Pages/TeamEventEditorPage.razor`
   - Replaced `@bind="_startTime"` and `@bind="_endTime"` on native HTML `type="time"` inputs with explicit `value` and `@onchange` callbacks.
   - Added `OnStartTimeChanged(ChangeEventArgs)` and `OnEndTimeChanged(ChangeEventArgs)`, which store browser-returned `HH:mm` strings in the existing fields.
   - This prevents Razor's time input binder from generating `TimeOnly?` conversion code against string fields, fixing the `CS1503` and `CS0029` pairs at lines 33/40.
   - `TryTime`, `TimeSpan` parsing, Gregorian persistence and event validation remain unchanged.

## Verification

- Confirmed all five reported error sites have been changed and no native time inputs remain bound to strings via `@bind` in these Razor files.
- Checked `ChangeEventArgs` is available through the project's existing Razor imports.
- Validated full and changes-only ZIPs via CRC integrity tests and checked they include only intended source modifications.
- **Not compiled in this environment:** .NET SDK is unavailable, so please build locally.

## Local build

From the unpacked project root:

```bash
dotnet restore
dotnet build
```

Verify the Jalali date picker opens and selects a day, and the event editor accepts start/end times before saving.

No additional EF migration is needed for this hotfix. Apply the existing Glasswork 2.22 migration if your database has not been updated yet.
