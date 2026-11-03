# Glasswork 2.24 — Event Cards, Smart Cards, and Reusable Content Browser

**Baseline:** Glasswork 2.23  
**Scope:** event widgets, birthdate occurrences, reusable content discovery, rotating composite dashboard cards.  
**Database schema:** unchanged (depends on the `BirthDate` and `CalendarEvents` migrations already introduced in 2.21/2.22).

## What changed

### 1. Content library categories

`DashboardContentBrowser` owns the **search bar, category rail, and result-card grid**. Its callers own their own selection state and footer actions.

- The existing `DashboardContentPicker` uses it for adding/removing dashboard tiles.
- `DashboardSmartConfigModal` reuses it for the smart card's ordered selections; there is no copied search/catalog markup.
- New category **رویدادها** contains four selectable cards: **رویداد انتخابی** (selectable event dropdown during dashboard edit), **رویدادهای امروز**, **رویدادهای این هفته**, and **رویدادهای این ماه**.
- New category **کارت هوشمند** contains the single **کارت هوشمند** template.
- The daily, weekly, and monthly event cards need **no configuration**. The week begins on Saturday; the month follows the Jalali calendar. The generic selected-event card can display recurring events, one-time past events, or a selected birthday.

Search/catalog access checks continue to use `IDashboardContentService`, which checks both dashboard access and each original content source's permission. New synthetic card templates are discovered through the same interface, with the Events permission required for event templates.

### 2. Birthdays in calendar and event cards

`CalendarEventService.GetOccurrencesAsync(...)` adds read-only **virtual birthday occurrences**, derived from non-deleted users with non-null `ApplicationUser.BirthDate`.

- Birthday date is stored as **native Gregorian DateTime**; the anniversary is calculated each year using the **same Jalali month and day** initially entered in the UI.
- An Esfand 30 birthday occurs only in Jalali leap years where that date exists.
- Virtual birthday occurrence IDs are negative user IDs. They are **not `CalendarEvents` records**: they are not editable through the event editor and are never duplicated in the DB.
- Birthday markers have warm gold styling and the friendly formal copy `🎂 زادروز … 🎉` in monthly, weekly, and daily calendar views. The year view stays the original event-free overview by design.
- Clicking a birthday opens its details but does not show the normal **مشاهده / ویرایش** action.
- Birthdays also appear automatically in day/week/month dashboard event cards and in the selected-event dropdown.

Events must pass the existing Events access check; the birthdate source is never changed by recurrence expansion.

### 3. Smart card lifecycle

1. Enter dashboard editing mode and choose the **کارت هوشمند** template from the regular picker.
2. The configuration modal opens for a newly added smart card. For an existing one, click **تنظیمات کارت** in editing mode.
3. Choose **انتخاب محتوا** to open the reusable content browser.
4. Select up to **30** items. Confirming the selection closes only this browser, returning to the configuration modal with a short list of the chosen items.
5. Reorder by dragging list rows; up/down controls provide a non-drag alternative. Remove items with the row's `×`.
6. Choose **افقی** / **عمودی** via the styled switch and enter the auto-advance interval (integer **1–3600 seconds**, default **3 seconds**).
7. **ذخیره تنظیمات** stages the settings inside the current dashboard edit transaction. The regular dashboard **ذخیره** commits them; **انصراف** restores the original layout.
8. The slideshow scrolls one card at a time with an animated CSS transform, loops around, pauses on hover, and allows manual slide navigation. Users with reduced-motion preferences get non-animated transitions.

The regular card is still independently movable/resizable by the freeform dashboard canvas. The slideshow fills that card's available space. SQL/cURL snippets, notes, todos, announcements, temporal event lists, and report charts are rendered in the slide. Nested smart cards and unconfigured single-event templates are deliberately not offered as child slides (preventing recursive cards and empty event selections).

### 4. Saved configuration

No new entity/table is necessary. The existing `DashboardContentReference` adds the optional `options` JSON object:

```json
[
  { "type": "events-week", "id": 1, "w": 3, "h": 6, "x": 0, "y": 0, "pw": 400, "ph": 290, "cw": 1200, "options": null },
  { "type": "event", "id": 1, "options": { "eventId": 48, "items": null, "horizontal": true, "intervalSeconds": 3 } },
  { "type": "smart", "id": 1, "options": {
      "eventId": null,
      "items": [ { "type": "note", "id": 123 }, { "type": "report", "id": 45 } ],
      "horizontal": false, "intervalSeconds": 5
  } }
]
```

`id: 1` is a **template identity within its card type**, not the ID of a persisted event. The selected event's actual ID is in `options.eventId`. Each top-level dashboard region can contain one of each template type. Optional `options` keeps all pre-2.24 layout JSON backward-compatible. Existing card geometry and Save/Cancel semantics remain intact.

Server-side `DashboardConfigurationService.VerifiedLayoutAsync(...)` validates option names, bounds, no recursive/nested options, uniqueness, and all child content references. Any configured child must be resolved through the permission-aware resolver. For **global** smart cards, only public content can be saved. For **shared user-created** dashboards, private child content remains redacted to unauthorized viewers without leaking its title, summary, code, author, or chart. No source content snapshot is stored in the layout JSON.

### 5. Existing UI behavior preserved

- Author-owned dashboard editing, Admin/Owner global editing, and per-viewer favorite settings remain unchanged.
- The separate selection preview and confirmation flow in the regular picker remains.
- The generic event dropdown appears **only during editing**; other event cards cannot be configured.
- A smart card's editing actions appear only to users permitted to edit that dashboard area.
- Existing chart gallery and report expand behavior are reused.
- The shared picker/card styling has been moved to clearly prefixed global CSS assets so Razor CSS isolation does not hide the layout inside the reused child component. Empty compatibility `.razor.css` files remain to make changed-files ZIP overlays safe.

## Primary changed files

**Application**
- `src/HealthMonitoringApp.Application/Dashboards/DashboardContentModels.cs`
- `src/HealthMonitoringApp.Application/Events/CalendarContracts.cs`

**Infrastructure**
- `src/HealthMonitoringApp.Infrastructure/Dashboards/DashboardContentService.cs`
- `src/HealthMonitoringApp.Infrastructure/Dashboards/DashboardConfigurationService.cs`
- `src/HealthMonitoringApp.Infrastructure/Events/CalendarEventService.cs`

**Web UI**
- `src/HealthMonitoringApp.WebUI/Components/Pages/Home.razor`
- `src/HealthMonitoringApp.WebUI/Components/Pages/DashboardTile.razor`
- `src/HealthMonitoringApp.WebUI/Components/Pages/DashboardContentPicker.razor`
- `src/HealthMonitoringApp.WebUI/Components/Pages/DashboardContentBrowser.razor` **(new)**
- `src/HealthMonitoringApp.WebUI/Components/Pages/DashboardEventCard.razor` **(new)**
- `src/HealthMonitoringApp.WebUI/Components/Pages/DashboardSmartCard.razor` **(new)**
- `src/HealthMonitoringApp.WebUI/Components/Pages/DashboardSmartConfigModal.razor` **(new)**
- `src/HealthMonitoringApp.WebUI/Components/Pages/TeamCalendarPage.razor(.css)`
- `src/HealthMonitoringApp.WebUI/Components/App.razor`
- `src/HealthMonitoringApp.WebUI/wwwroot/css/dashboard-content-library.css` **(new)**
- `src/HealthMonitoringApp.WebUI/wwwroot/css/dashboard-card-components.css` **(new)**
- `src/HealthMonitoringApp.WebUI/wwwroot/css/dashboard-smart-events.css` **(new)**
- `src/HealthMonitoringApp.WebUI/Components/Pages/DashboardTile.razor.css` (compatibility stub)
- `src/HealthMonitoringApp.WebUI/Components/Pages/DashboardContentPicker.razor.css` (compatibility stub)

## Required local test plan

1. Run `dotnet build` at the solution root. These changes have **not** been compiled against .NET inside the packaging environment.
2. Log in as Owner/Admin. Add all four event card types to the global area; pick an event in editing mode, Save, reload, and confirm the selection persists and the dropdown disappears outside edit mode.
3. Log in as a regular user with Events access. Verify that the automatic cards list only today's/current Saturday–Friday week/current Jalali month occurrences. Verify annual and repeating events as well as birthday markers.
4. Enter a known Jalali birthdate for an active user in profile settings. Check the anniversary in a calendar month/week/day, an automatic event card, and the selected-event dropdown. Confirm that clicking it does not offer a broken edit URL. Try Esfand 30 in leap/nonleap years.
5. Add a smart card, select multiple notes/todos/SQL/cURL/reports, move their priorities, change direction and timing, then Save/reload. Check 3-second default and custom timing, pause on hover, manual navigation, and smaller/larger resized dashboard cards.
6. Verify **Cancel** in the smart configuration leaves the previous card state intact. Verify dashboard **Cancel** restores previous options even if the smart configuration modal was saved.
7. With two users, include a private note or SQL in a user's shared smart card. The author should see it; an unauthorized viewer must see a locked, redacted slide **with the same slot**. Confirm a private child cannot be saved into a global smart card.
8. Test the content browser within both modals at desktop and mobile widths, including full-text search and category switching.
9. Exercise reports in the smart carousel, including switching away/back and a narrow dashboard tile. Check the responsive chart and expand action.
10. Confirm old 2.23 layouts still load and that one existing dashboard can be edited and saved without changing its other cards.

**Verification here:** project XML, CSS parsing, JavaScript syntax, source structure, and ZIP integrity. **Not performed here:** .NET/Razor compilation, EF/database-backed tests, SignalR browser interaction tests, and visual regression tests in a running CENOPS instance.
