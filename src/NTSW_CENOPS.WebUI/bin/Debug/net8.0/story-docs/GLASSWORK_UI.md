# NTSW CENOPS — Glasswork workspace shell

The redesign is limited to the authenticated app and uses the existing components,
routes, role policies, Identity services, and CRUD logic unchanged.

## Design system

- `src/HealthMonitoringApp.WebUI/wwwroot/css/operations-shell.css` contains the
  authenticated workspace tokens and shared component treatments under `.app-shell`.
- `src/HealthMonitoringApp.WebUI/wwwroot/css/operations-refinements.css` is the
  Glasswork polish layer: accessible text contrast/sizes, colored actions, RTL
  row-action placement, Customs branding and popup layering. Both files are only
  applied under the authenticated `.app-shell`.
- `Components/Layout/MainLayout.razor` owns the workspace composition and breadcrumb.
- `Components/Layout/Skyline.razor` owns brand, *existing* animated title, future
  workspace switch, account/settings actions, and logout confirmation.
- `Components/Layout/NavMenu.razor` retains the original role-aware Sidebar items.
  It is presented as an icon-first dock on desktop; hovering or keyboard-focusing
  the dock expands labels. It becomes a full drawer on smaller screens.
- `Components/Layout/QuickNavigate.razor` is the live role-aware page navigator.
  Press Ctrl+K / Command+K or click its button; it searches app routes, not DB rows.
- `Components/Pages/Home.razor(.css)` is a data-ready dashboard without fake KPIs.
- Table styles, AppModal, tabs, context menus, pagination, and input overlays
  share the same shell-specific styling, without altering their behavior.

## Future chat workspace

The Skyline switch is intentionally a visual stub for the Chat workspace:
Operations navigates to the existing dashboard; Chat is disabled and does not
navigate to a fake route. When chat is implemented, add a real route and role
policy first, then convert the disabled switch item into a NavLink and switch
sidebar menu sources per workspace in NavMenu (or an extracted WorkspaceMenu).

## Testing checklist

- Login/signup/change-password: visually unchanged.
- Header: animated title, profile menu, edit profile, change password, settings
  icon (role gated), and logout confirmation with antiforgery form still work.
- Desktop: sidebar expanded on hover/keyboard focus. Mobile: hamburger drawer,
  backdrop close, and close after choosing a link.
- Search (click and Ctrl/Command + K): only accessible routes appear; pressing
  Escape or clicking outside closes it.
- Settings: tabs, Users/Roles CRUD, role-dependent visibility, filters,
  pagination, context menus, and the Persian assigned-role deletion warning.
- Other DataTables: filters, row selection, create/edit/delete, page controls.
- Filter popovers are viewport-anchored using `cenopsUI.getFilterPosition()` in
  `wwwroot/js/site.js`: only the trigger button (not the fullscreen transparent
  backdrop) receives toolbar button styling. Verify the panel does not shift/hide
  the table or page header,
  and flip upward if clicked near the bottom of the viewport.
- Row ellipsis is the first column in RTL (right edge); its opened context menu
  must layer over the click-away backdrop.
- Sidebar footer shows «گمرک», not a generic N icon. Monitoring group icon is
  centered both collapsed and expanded; there is no sidebar top badge.

No schema modifications and no migration. `dotnet build NTSW_CENOPS.sln`
was not possible in this environment (dotnet SDK not installed), so a local
build and interactive UI test are still required.
