# Glasswork 2.13.1 — Controls and Knowledge UX Refinement

Base: Glasswork 2.13. No EF migration or service model changes.

## Changes

1. **Todo checkboxes** use green (`#218c60`) instead of the previous theme. The editor rows, spacing, input height, and padding were made more compact without changing todo state or drag/reorder behavior.
2. **Announcements** use a distinctive raspberry/rose accent (`#bd4864`) instead of brown. Cards, new-card callout, author avatar, primary action, and Skyline/sidebar icons use the same palette.
3. **Checkboxes** share a single accessible, native-input CSS skin with visible checked, disabled, and focus states. `Components/Common/CheckBox.razor` was simplified; `Value`/`ValueChanged`/`Disabled` remain compatible and the async event handler now returns `Task` rather than `async void`. The shell's hidden navigation toggle is explicitly excluded.
4. **All existing single-select dropdowns** gain a searchable custom option popover via `wwwroot/js/searchable-select.js` and `wwwroot/css/glasswork-controls.css`. The original `<select>` DOM node stays in place and remains the binding source. Choosing an option sets `select.value` and dispatches a real bubbling `change` event, so Blazor `@bind`, `@onchange`, form submission, filtering, and disabled options keep working. Only one popup opens at a time. Keyboard interactions: arrows, Enter, Escape and Tab. The enhancement is progressive; without JS, normal native selection remains available. The options popover attaches to `document.body` to avoid interfering with Razor DOM reconciliation. Search options are rendered via `textContent` (not HTML injection).
5. **Skyline Ctrl+B → T / A** is fixed by adding the two new shortcuts to the existing authorized command handler, including the chat-modal guard.
6. **Skyline Todo/Announcement icons** are the same actual SVG paths as their respective sidebar items; feature colors align with the new palettes.
7. **Inline Announcement creation** is positioned absolutely relative to its original creation tile; that tile stays in the grid at its original height. Scroll stays inside the floating editor on smaller viewports.

## Source files

- `src/HealthMonitoringApp.WebUI/Components/App.razor` — assets and cache busting
- `src/HealthMonitoringApp.WebUI/Components/Common/CheckBox.razor` — shared checkbox
- `src/HealthMonitoringApp.WebUI/Components/Announcements/AnnouncementLibrary.razor` — anchored creation panel
- `src/HealthMonitoringApp.WebUI/Components/Layout/Skyline.razor` — SVG icons
- `src/HealthMonitoringApp.WebUI/Components/Layout/Skyline.razor.css` — icon colors
- `src/HealthMonitoringApp.WebUI/wwwroot/css/knowledge-library.css` — announcement theme and floating positioning
- `src/HealthMonitoringApp.WebUI/wwwroot/css/glasswork-controls.css` — new shared controls styling
- `src/HealthMonitoringApp.WebUI/wwwroot/js/searchable-select.js` — new progressive shared searchable dropdown
- `src/HealthMonitoringApp.WebUI/wwwroot/js/site.js` — keyboard shortcuts and Escape priority

## Testing

Chromium smoke checks: searchable dropdown filtering and selection (click/Enter), Todo accent inheritance, anchored Announcement editor not increasing the grid row, Ctrl+B/T, Ctrl+B/A, and Escape dismissing an open select before its parent modal.

`node --check` passed for both new and updated JS modules. Full solution .NET build and live Blazor tests were not available in this environment; compile locally before deployment.

## Suggested local test cases

- Test dropdowns in the report wizard, job settings, Admin permission filter, author selection, and DataTable pagination/filter. Verify real bound values change and disabling a dropdown makes it non-interactive.
- Test all checkbox variants, including DataTable table rows and disabled checkboxes; Todo green in edit and preview, cyan everywhere else.
- Press Ctrl+B, wait for the menu, then T or A; verify actual navigation and your server access policy.
- Open inline Announcement while several cards occupy its row. Check the row height does not change and that the editor remains usable on narrow screens.
- Run a fresh and an existing Todo/Announcement create/edit/save action. No schema migration needed.
