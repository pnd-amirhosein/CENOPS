# Glasswork 2.33 — Dashboard privacy warning, note-card clipping, event icon alignment and note palette

**Baseline:** Glasswork 2.32. A focused maintenance release; no entities, services, migrations, or persisted layout structures have changed.

## Changes

### 1. Unavailable-card confirmation is scoped to the selected user dashboard

`Home.razor` — `HasUnavailableCardsAsync` now checks only `_personalRefs` belonging to the currently selected and editable user dashboard. It also inspects the selected dashboard's nested smart-card references, using the same permission-aware resolver.

**Global dashboard references are not checked** by this warning flow, even for Admin/Owner editors. The pre-existing server-side permission enforcement and global-content validation remain unchanged. Existing deleted/private references still render as protected placeholders and can be retained when saving, subject to the existing confirmation flow. The user must still be permitted to edit the selected dashboard for the warning to apply.

### 2. Note dashboard cards crop rather than scroll

`wwwroot/css/dashboard-card-components.css` — the note content viewport now has `overflow: hidden`, a flexible constrained height, and no custom scrollbar. Inline note rows still render using `NoteContent` and `RowGroup`; text, tables, and code beyond the visible tile bounds are cropped. Nested code snippets and the note-reading surface do not add internal scrolling in compact tiles.

The existing **مشاهده محتوا** action still opens the full note preview, where the original reading/scrolling behavior is unchanged. The clipping also applies to note slides inside smart cards.

### 3. Selected-event fact icons use stable SVGs

`DashboardEventCard.razor` and `wwwroot/css/dashboard-smart-events.css` — replaced the font glyphs for start, end, recurrence, and next occurrence with appropriately sized stroke icons. The common icon container uses `inline-flex`, explicit dimensions, and centered placement. Information blocks still switch to two columns in sufficiently wide cards.

### 4. Note insertion menu uses orange theme

`wwwroot/css/note-workspace.css` — retained the existing icons and menu interactions. The icon tiles now use the note theme's amber foreground, warm cream background, and matching hover/focus accents.

## Modified source files

1. `src/HealthMonitoringApp.WebUI/Components/Pages/Home.razor`
2. `src/HealthMonitoringApp.WebUI/Components/Pages/DashboardEventCard.razor`
3. `src/HealthMonitoringApp.WebUI/wwwroot/css/dashboard-card-components.css`
4. `src/HealthMonitoringApp.WebUI/wwwroot/css/dashboard-smart-events.css`
5. `src/HealthMonitoringApp.WebUI/wwwroot/css/note-workspace.css`

## Validation checklist

- [x] Compare against the exact 2.32 ZIP to ensure only the five intended source files and this guide changed.
- [x] Parse modified CSS with `tinycss2`.
- [x] Parse all four event SVGs as XML.
- [x] Assert the confirmation resolver never checks global references and still handles nested smart-card content.
- [x] Verify note preview clipping and orange icon styles exist.
- [x] Validate both ZIP packages and their contents.
- [ ] Build the complete solution locally with the .NET SDK.
- [ ] Verify in Blazor that saving a personal dashboard doesn't warn about unavailable global cards.
- [ ] Verify unavailable cards in the selected personal dashboard, including nested smart cards, still prompt for confirmation and preserve positions.
- [ ] Resize note cards and smart-card note slides; ensure content clips and **مشاهده محتوا** still opens the full note.
- [ ] Resize selected-event cards; verify SVG alignment and responsive two-column layout.
- [ ] Check note insert-menu icon color and keyboard focus in light and dark mode.

No database migration is needed. The existing 2.32 permission and persistence rules remain in place.
