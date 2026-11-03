# Glasswork 2.40 — Shared Page Header Actions

Baseline: Glasswork **2.39**. No database migration, dependency addition, or configuration change required.

## Objective

Move page-specific commands and filters out of redundant local toolbars into the existing CENOPS application header. The shell now shows **گمرک / سیناپس** instead of **محیط عملیاتی / CENOPS**.

## Architecture

Uses Blazor `Microsoft.AspNetCore.Components.Sections` instead of a bespoke action-store or JavaScript DOM relocation:

- `MainLayout.razor` declares one `<SectionOutlet SectionName="cenops-page-actions" />` in the workspace header.
- Each page (or wiki library component) publishes controls with `<SectionContent SectionName="cenops-page-actions">`.
- The Blazor section system handles render updates and removes the controls automatically when a page is disposed/navigated away; event handlers and state belong to their originating pages.
- `PageHeaderSearch.razor` is a reusable two-way bound `Value` / `ValueChanged` search input with `Label` and `Placeholder` parameters, used by the wiki and report galleries.
- `wwwroot/css/page-header-actions.css` is globally loaded since rendered header content lives under the layout, and implements responsive wrapping and consistent command/search styling.

Example for new pages:

```razor
<SectionContent SectionName="cenops-page-actions">
    <PageHeaderSearch @bind-Value="_query" Label="جستجو" Placeholder="نام..." />
    <button type="button" class="page-action-button" @onclick="RefreshAsync">بروزرسانی</button>
</SectionContent>
```

Only publish actions for permitted operations; backend permission checks remain authoritative. Do not register static/global handlers that outlive the page.

## Page-specific changes

| Area | Header action(s) | Removed from page |
| --- | --- | --- |
| `/tools/source-explorer` | `مدیریت منابع و ایندکس` | Entire local source header, including overline, redundant title and description |
| `/tools/source-indexes` | `بروزرسانی پوشه‌ها`, link to symbol search | Entire local management header |
| `/dashboard` | Edit, Save, Cancel, default star, selector, create/delete, separate scoped global/personal Add Content | Old dashboard top toolbar and both in-section Add Content buttons |
| `/reports` | Search and `مدیریت گزارش‌ها` (permission dependent) | Redundant report gallery header/toolbar |
| `/team/events/list` | Calendar link | Calendar link from the event-list heading |
| `/tools/clean-code` | Save as... | Entire cleanup header |
| `/wiki/notes`, `/wiki/todos`, `/wiki/announcements`, `/wiki/curls`, `/wiki/queries` | Live search, via shared PageHeaderSearch | Individual search bars from the body |

Existing table-level CRUD/filter actions in management tables are **not** moved. Their DataTable toolbar remains contextual to the table.

## Testing checklist (run locally)

1. Build: `dotnet build`. Verify Razor section components, library searches, and editor bindings compile.
2. Navigate repeatedly between dashboard, wiki libraries, tools, and reports. Each header should show **only the current page's actions**, without duplicates or stale controls.
3. Dashboard: switch dashboards; confirm star, edit, create/delete, scoped global/personal add, Save/Cancel and confirmations continue to work. User permissions must still control global editing.
4. Reports: search while charts load and after loading; verify manager-only link updates after permission resolution.
5. Wiki: search each of the five libraries; ensure filtering remains reactive after switching routes.
6. Source indexes: test refresh while busy and navigation to the explorer. Source explorer: search, expand definitions, and navigate back.
7. Code cleanup: Save as... opens with the current code and returns focus to the header button when closing.
8. Check 1440px, tablet and narrow mobile widths. Header actions should wrap, without clipping or horizontal overflow.
9. Verify Hebrew/Persian RTL text layout, tab navigation, and focus styling.

## Verification performed

- Source-level route/control placement checks, shared search bindings, layout label, and balanced section tags.
- CSS syntax validation with `tinycss2`, project XML parsing.
- Chromium HTML/CSS fixture at 1440px, 1080px, 760px and 390px: no horizontal overflow. This is a **visual layout fixture**, not a live Blazor integration test.
- A real .NET/Razor build and database-backed Blazor functional test could not be performed here; run these locally before deployment.

## Unchanged

No changes to persistence, dashboard JSON, authorization services, event services, source indexing, or data models. No migration required.
