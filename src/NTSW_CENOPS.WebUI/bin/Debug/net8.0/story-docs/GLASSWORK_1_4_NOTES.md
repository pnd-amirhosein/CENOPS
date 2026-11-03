# Glasswork 1.4 — finishing fixes

- DataTable renders a Persian empty-result state when there are no items and no new row being created.
- User account popup buttons are compact single-line menu rows (visual-only adjustment).
- Replaced the old GIF-based/JS-toggled global loading mechanism with reusable `AppLoading` variants: `inline`, `overlay`, `page`.
  - MainLayout: page/navigation transitions.
  - Administration: Identity data fetch.
  - DataTable: real CRUD and RefreshData operations with try/finally busy cleanup.
  - Contacts: removed fake three-second delay; HealthApis demo: removed artificial JS loading toggle.
- AppTabs now owns immediate selected-state feedback and raises two-way `ActiveKeyChanged`; Administration binds `@bind-ActiveKey` to the selected table.
- All gradient-based *button* backgrounds in the final theme layer are now solid cyan. Decorative surface gradients remain unchanged.

No schema or migration changes.

## Applying only changed files
The changed-files ZIP includes `REMOVE_THESE_FILES.txt`: delete those paths from your existing checkout; an overwrite extraction alone does not delete them. The full ZIP already has the deletions applied.

## Verification
Checked the relevant source invariants, CSS syntax using tinycss2, and `node --check` for site.js. No .NET SDK or interactive Blazor browser available in the environment; local `dotnet build NTSW_CENOPS.sln` and runtime testing remain necessary.
