# Glasswork 2.25 — Dashboard Content, Event Cards & Smart Card UX

**Baseline:** `NTSW_CENOPS_Glasswork_2_24_1` (the CS0854 fix is included).  
**Database:** no new migration. The 2.21 birthdate and 2.22 calendar migrations are still required if not previously applied.  
**Framework:** .NET 8 / Blazor / EF Core.  
**Verification scope:** static source, JS syntax, CSS parse, XML parse, ZIP integrity. **Full .NET/Razor compilation and SQL integration tests have not been run in this environment.**

## 1. Dashboard content-picker scope

`Home.razor` no longer renders a single ambiguous Add Content control in the global action bar. In dashboard edit mode:

- **داشبورد عمومی**: the adjacent Add Content button is shown only if `CanEditGlobal` is true.
- **User-created dashboard**: the adjacent Add Content button is shown only to the selected dashboard creator (`Selected.CanEdit`).
- The picker receives an explicit `TargetGlobal` Boolean instead of showing two destination actions in its footer.
- The selected list is initialized from **only the target dashboard**. Editing the global list does not silently mutate the personal list, and vice versa.
- When adding to a personal dashboard, already-global content remains disabled/locked. Global editing may remove global references because only an authorized Admin/Owner can open that picker.
- Global picker search is `PublicOnly`; final writes also pass through the existing server-side content authorization.
- Previously selected cards are more visibly muted in the library while staying legible. Selection state remains expressed through badges and accessible pressed labels, not color alone.

## 2. Multiple independent smart cards

Previous builds used the fixed ID `smart:1`, so a second instance collided with the first. Each new smart-card instance now receives a **unique positive ID in the selected dashboard** (`smart:2`, `smart:3`, …). The repository's reference resolver returns the special smart-card template with the requested instance ID while keeping authorization on the same dashboard permission.

- Each smart card has its own saved position, dimensions, ordered content IDs, direction, and interval in `LayoutJson`.
- The browser's smart-card tile becomes an **add another instance** action. It displays the count of smart-card instances already staged.
- The existing no-duplicate rule for ordinary wiki content and reports is unchanged.
- In the selection preview, each instance can be removed separately.
- Saving the dashboard persists each instance; subsequent opens resolve all its IDs independently.

**Note:** Smart cards remain normal layout items with no additional database entities. Do not replace the positive ID with `0` or negative IDs: the server validator rejects those.

## 3. Event cards & details

- The single-event card dropdown excludes `IsBirthday` and nonpositive IDs; the server layout validator also rejects a nonpositive configured `EventId`.
- A single-event card shows **one selected event**, with a prominent title, author hover component, Gregorian-stored/Jalali-displayed start and end times, repeat description, and one details action. Repeating occurrences are not rendered as a duplicate list.
- Daily/weekly/monthly cards show only occurrences for their respective *current* window. Those may include virtual birthday occurrences, which have a restrained gold celebratory style.
- Each listed event has a details button, Jalali date/time and `UserHoverName` author entry.
- A compact calendar link is available from the event card.
- Recurrence presentation is shared via `CalendarRecurrenceLabel` and includes interval, selected weekdays, month days, selected months, calendar basis, and end limit when provided.
- Event details use the root-level `DashboardEventDetails` component. The callback is forwarded from `DashboardEventCard` through `DashboardTile` or `DashboardSmartCard` to `Home.razor`, so the modal is never clipped by a transformed smart-card track.

The database continues to store native Gregorian `DateTime` values; recurring events are still expanded from saved recurrence rules.

## 4. Smart-card experience

- The smart card **has one outer dashboard card**, not another nested bordered frame or an additional feature header.
- The outer right-hand accent follows the currently visible content type.
- Dots and previous/next controls sit over a dark lower-edge gradient, visible on hover/focus (and on touch devices).
- Dot pagination is a sliding window of at most seven, so many selected cards don't overflow a narrow dashboard tile.
- The auto-advance interval is still configurable from **1–3,600 seconds**; default remains **3 seconds**. Hover pauses advance, reduced-motion preference disables visual track animation.
- Smart configuration has a common settings grid for direction and interval, an ordered card list, grab-to-reorder with drop-target feedback, and a hover/focus-revealed trash icon. Redundant up/down buttons were removed.
- Configuring does not persist immediately; **dashboard Save** still commits, and **dashboard Cancel** restores the previous layout.

## 5. Modal close behavior

- The main picker, its content preview, smart-card configuration, its nested browser, and the report's floating close affordance now use `ESC` keycap styling rather than an uncentered X.
- The existing picker unsaved-selection warning is preserved; Escape invokes the same flow as the visible close control.
- Nested smart configuration handles Escape in this order: open details preview → content browser → configuration.
- Dashboard removal confirmations cancel safely with Escape.
- Chat dialog close affordances use the same ESC style. The chat keyboard listener prefers an open nested dialog/search/details pane rather than closing the entire workspace prematurely.
- Native `AppModal` continues to respect its `CloseOnEscape` policy; sensitive dialogs that intentionally suppress Escape remain protected.

## 6. Calendar visual pass

Refined calendar toolbar, mode controls, selected/current-day contrast, month event chips, timeline blocks, birthday highlights, hover feedback, and border colors. The existing year/month/week/day structure, Jalali date handling and internal scroll behavior are unchanged.

## Changed source areas

- Dashboard page, tile, picker, shared browser, preview, smart renderer and smart configuration.
- Shared event presenter (`DashboardEventDetails.razor`, `CalendarRecurrenceLabel.cs`).
- Dashboard configuration/content services for safe special-card identities and validation.
- Shared modal and chat close controls.
- `site.js`, dashboard content library/smart/calendar/chat styles.

## Local integration checklist

1. Run `dotnet build` at the solution root; resolve any Razor/compiler messages before applying to production.
2. In edit mode, verify separate Add Content buttons and target-specific counts on Global versus a user-created dashboard. Confirm normal users cannot add to Global or edit someone else's dashboard.
3. Add three smart cards to one dashboard; give them different content, scroll directions and intervals. Save, reload, and verify identity, appearance, controls, geometry and independence.
4. Remove a smart card via the picker preview and via dashboard trash, then save. Verify the other instances remain.
5. Confirm global cards are disabled in a personal picker and personal cards can be toggled. Check against two accounts with different content permissions.
6. Set up normal and repeating events. Verify single-event dropdown has no birthdays; daily/weekly/monthly cards include upcoming birthday occurrences.
7. Verify event details on a normal card **and inside a smart card**, including recurrence rules and user hover/DM behavior.
8. Test ESC in picker with unsaved selections, nested smart browser and content preview, chart expand modal, dashboard removal confirmation, and nested chat dialogs.
9. Check calendar year/month/week/day at desktop and narrow widths. Verify scroll is view-local, not an accidental outer page scrollbar.
10. Test short/tall/wide smart cards and reduced-motion setting, especially reports and event details while auto-slide is active.

### Deployment

No schema change is introduced by 2.25. Preserve all previously applied migrations. Deploy either the full archive or copy the changed-files archive over your matching 2.24.1 tree. Keep a backup and run normal build, access and smoke tests before promoting the changes.
