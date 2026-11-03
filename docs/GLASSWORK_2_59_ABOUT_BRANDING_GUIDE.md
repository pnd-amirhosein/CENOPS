# CENOPS 2.59 — GLASSWORK

## Summary
- Release naming standardized as **CENOPS** (product), **2.59** (version) and **GLASSWORK** (codename).
- Added an unrestricted information action to the Skyline between Settings and Logout.
- Centered About window uses the reusable `AppModal`, shared cyan CENOPS logo, release details, credits, copyright, and the supplied ILECY brand mark in original `#0066FF`.
- The About dialog renders through a layout-level section outlet so it cannot be clipped by Skyline's glass shell.
- Graph image export branding and the cache-busted graph script now display 2.59.
- Moved all project Markdown files into `/docs/`; future patch notes belong here.

## New files
- `Components/Common/CenopsMark.razor` — single source of the existing CENOPS vector mark.
- `Components/Common/CenopsRelease.cs` — release display metadata.
- `Components/Common/CenopsAboutDialog.razor` + `.razor.css` — About dialog.
- `wwwroot/img/ilecy-mark.png` — user-supplied ILECY logo, unchanged.

## Verification checklist
1. Confirm the info icon is between Settings and Logout for every user role.
2. Open the About dialog, close with the floating close control, Escape, and backdrop.
3. Confirm the cyan logo matches Skyline and the ILECY mark and text use `#0066FF`.
4. Confirm the dialog stays centered and above the whole shell, including on smaller screens.
5. Verify `dotnet build` and live authentication/authorization locally (not available in the packaging environment).
