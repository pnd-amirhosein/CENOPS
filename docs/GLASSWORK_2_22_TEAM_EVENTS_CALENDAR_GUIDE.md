# Glasswork 2.22 — Team Events & Calendar

Baseline: **Glasswork 2.21**. Scope: favorite-star color, team event model/services, recurrence rules, Jalali calendar, new event editor, team navigation and `E` shortcut.

## 1. Dashboard favorite star

The default-dashboard star is now **neutral/dark gray when inactive** (unfilled SVG) and **gold when selected** (filled SVG). The per-viewer selection/default persistence introduced in 2.18 was not modified.

## 2. Routes, navigation and permissions

- `/team/events` — calendar with **سال / ماه / هفته / روز** views.
- `/team/events/new` — new event editor; accepts `?date=yyyy-MM-dd` (native Gregorian query value).
- `/team/events/edit/{id}` — edit or soft-delete an event.
- Sidebar: **مدیریت تیم → زمان و رویدادها**, above **دانش‌نامه توسعه**.
- Skyline + dropdown: **رویداد جدید** with **E** shortcut **only while the + menu is open**, using the same Blazor click handler and live permission recheck as other creation actions.
- Global quick-navigation search includes **زمان و رویدادها**.
- Stable permission `team.events.view` (`AccessKeys.Events`) gates both pages and all service methods; never trust sidebar visibility for authorization. Existing built-in **User/Admin/Owner** roles receive this permission in the migration. Custom roles may be granted access through **مدیریت دسترسی‌ها**.
- All users with this permission can see team events and create their own. Creator or Admin/Owner may edit and delete. Deletes are soft deletes and disappear from calendar queries.

## 3. Data model and service

`CalendarEventEntity` stores **one definition per event**, not individual repeat instances:

| Property | Meaning |
|---|---|
| `Title`, `Description` | Text (max 180/3000 chars) |
| `StartsAt` | Required Gregorian `DateTime` (SQL `datetime2`) |
| `EndsAt` | Nullable Gregorian `DateTime` for range events |
| `Repeat` | `None`, `Daily`, `Weekly`, `Monthly`, `Yearly` |
| `Interval` | Every 1, 2, 3… days/weeks/months/years |
| `WeekdaysMask` | Sunday=bit 0 … Saturday=bit 6; 0 uses event start weekday |
| `MonthDays` | Optional comma-separated days 1–31; default uses start day |
| `Months` | Optional comma-separated months 1–12; annual default uses start month |
| `CalendarBasis` | `Jalali` (default) or `Gregorian` for month/year recurrence semantics |
| `RepeatUntil` | Optional inclusive Gregorian ending date for recurrence |
| `CreatedById`, `IsDeleted` | Authorship and soft deletion |

`ICalendarEventService` is in Application, `CalendarEventService` in Infrastructure, and `EventRecurrence` expands occurrences for a **bounded requested period**. A full year is supported; the service caps requests at 370 days. Recurring events can be edited without rewriting existing rows. Multi-day occurrences are included when they intersect the requested window. Event spans are limited to **31 days** in this initial version to keep rendering/query costs bounded.

### Recurrence examples

- **Every two weeks on Saturday and Monday**: `Repeat=Weekly`, `Interval=2`, select Saturday and Monday.
- **Every 3rd and 13th of a month**: `Repeat=Monthly`, `Interval=1`, `MonthDays=3,13`, `CalendarBasis=Jalali` for شمسی month days, or `Gregorian` for میلادی month days.
- **January annually**: `Repeat=Yearly`, `CalendarBasis=Gregorian`, `Months=1`, with optional `MonthDays` (otherwise the event start day). This means selected **dates in January each year**, not every day of January by default. To schedule every day in January, use a separate recurrence policy/series.
- **Weekly only one weekday**: leave weekday selection empty to use the start weekday, or explicitly check it.

The editor accepts Western, Persian, and Arabic-Indic digits in multi-day numeric recurrence lists. Only real dates occur: a day 31 rule will not create a nonexistent day in shorter months. Week intervals are anchored to Saturday-based calendar weeks.

## 4. UI architecture

`CalendarDates` is a reusable presentation-only Jalali navigation utility, backed by the existing `JalaliDate` helper. Year view presents all 12 Persian months; month view shows a full 7-column event grid; week/day views show hour-by-hour scrollable timelines, with the day view providing a mini-month selector. Clicking an event opens a details modal with a link to its editor. Responsive CSS lives beside the new Razor pages and uses the existing cyan/neutral design language.

The editor uses the existing `AppJalaliDatePicker` for start/end/repeat-until dates and a native time field for the clock portion. The picker was extended to allow navigating up to 20 future Persian years (or explicit Min/Max bounds). **No Jalali text or date type is persisted.** Every service/model/database value remains native Gregorian `DateTime`.

**Time zone scope:** this first version intentionally stores wall-clock, `DateTimeKind.Unspecified`-style values and does not add cross-time-zone event conversion or reminders. Align the application/server time-zone policy before scheduling international team meetings. This is not a separate recurrence worker; occurrences are calculated when the calendar is requested.

## 5. Database migration required

`src/HealthMonitoringApp.Infrastructure/Migrations/20261015190000_AddCalendarEvents.cs`

Creates `CalendarEvents`, its author foreign key, date/index columns, and inserts the event permission if missing. Grants it to *existing* built-in User/Admin/Owner roles while preserving any custom role policy. Model snapshot updated. **Back up the database before migrating.**

From the solution root:

```bash
dotnet build
dotnet ef database update --project src/HealthMonitoringApp.Infrastructure --startup-project src/HealthMonitoringApp.WebUI
```

No changes to stored dashboard layouts, existing users, previous migrations, or chart data.

## 6. Suggested local integration tests

1. Run the .NET build and migration. Verify `CalendarEvents` table and `team.events.view` access record/role links, including any custom roles.
2. Click the dashboard default star: gray when not default, gold when selected. Switch dashboards to confirm correct color.
3. Open **مدیریت تیم → زمان و رویدادها** and navigate year, month, week and day in Jalali, including Esfand / Farvardin and short Persian months.
4. Open Skyline +, press **E**; create a single event. The event should appear on its selected day and time in all views.
5. Create a start/end event across midnight or a Persian-month boundary and confirm the event spans the correct days.
6. Create an every-2-weeks Saturday/Monday event; confirm its occurrence schedule in consecutive weeks and the absence of intermediate weeks.
7. Create monthly 3,13 rules, check Persian and Gregorian basis independently; test 31 on a month with fewer than 31 days.
8. Create yearly January recurrence with Gregorian basis, then test Jalali year recurrence. Edit and delete a series; confirm all future occurrences update/disappear.
9. Log in as another permitted user; confirm they see shared events, but cannot edit/delete other people's events. Admin/Owner can manage them.
10. Deny `team.events.view` for a role and verify route and service authorization fails closed.
11. Check calendar overflow at desktop/tablet/mobile widths, including busy days and long event titles.
12. Check birthdate modal still blocks future dates via its existing Max bound. Confirm date forms and report filters still return Gregorian to existing services.

## 7. Verification status

Performed here: ZIP integrity, file path checks, JavaScript syntax (`node --check`), CSS syntax parsing, .csproj XML parsing, and source-level wiring assertions. **Not performed:** .NET 8 compilation, EF migration execution, SQL Server integration, Razor rendering inside the actual application, accessibility audit, or browser workflow tests against Blazor. These must be done locally; the checks above are not replacements for a real build.
