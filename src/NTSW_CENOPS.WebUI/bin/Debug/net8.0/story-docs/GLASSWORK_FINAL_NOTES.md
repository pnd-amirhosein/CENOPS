# Glasswork final polish — October 2026

Changes are layered on `Glasswork Refined`, without modifying auth endpoints, migrations, models, or existing CRUD service contracts.

1. `AppTabs` is reusable, icon-first, RTL, animates the selected tab to show its Persian title. Use `AppTabItem.IconSvg` (trusted static application SVG), `Title`, `Key`, and optionally `AriaLabel`.
2. `DataTableToolbar` keeps the filter trigger at the far left of the RTL toolbar.
3. Filter popup matches the shared glass/cyan design, including labels, fields, actions and responsive layout.
4. Inputs use a border-color focus state instead of an outline/Tailwind focus ring. Keyboard focus for tab buttons retains a border state.
5. Remove-row buttons fade/slide in on hover or keyboard focus, and are not displayed for the sole remaining filter; touch devices show eligible remove controls.
6. Applying/clearing Settings filters performs a fresh `GetUsersAsync` or `GetRolesAsync` request through `IIdentityAdministrationService`; the refreshed result is filtered and paginated. The pre-existing Health APIs **demo page** uses in-memory rows rather than a database loader; Apply/Clear now re-evaluates its complete current source list (including CRUD edits). Connecting that page to a real API/database is a separate future feature.
7. Applied-filter counts appear on filter buttons, with no badge for zero active filters.
8. User-account dropdown styling matches glass popovers.
9. Existing Login, Signup and Change Password pages retain their actual forms, logo, background image and antiforgery tokens with an updated responsive layout. No password-reset page exists in this baseline.
10. Navigation parent and child icons now share the same center in collapsed desktop dock; full sidebar content remains available on mobile.
11. Search keyboard hint now reads `CTRL K` in a light treatment; the existing Ctrl/Command+K shortcut is unchanged.

## Validate on local .NET SDK

From the solution root:

```shell
dotnet build NTSW_CENOPS.sln
```

Then verify `/settings` filters/CRUD, `/health-apis` demo filtering, sidebar on desktop/mobile, account dropdown, and the three auth screens. No schema changes, so no migrations are needed.
