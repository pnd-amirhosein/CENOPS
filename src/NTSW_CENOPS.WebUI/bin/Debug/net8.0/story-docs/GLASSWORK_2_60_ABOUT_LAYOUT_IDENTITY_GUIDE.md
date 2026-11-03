# C.E.N.O.P.S. 2.60 — GLASSWORK

## Overview

- Redesigned the About dialog into a responsive two-column identity presentation.
- Removed the generic «درباره سامانه» modal header. The visible product name is the dialog content heading, and the modal keeps an independent accessible label.
- The shared cyan `CenopsMark` is presented on the right and stretched into the same row as the left information column. Small screens stack the logo above the information for readability.
- The left column contains «گمرک / سیناپس», the dotted English name `C.E.N.O.P.S.`, product description, release version and codename, and Digital Ecommerce / Customs development credits.
- Added two succinct Persian intellectual-property notices referencing `NTSW C.E.N.O.P.S.`, `NTSW Central Operations`, «سیناپس», «عملیات‌های مرکزی سامانه جامع تجارت» and the system's visual mark.
- Kept the divider, © 2026 notice, and compact ILECY signature at the bottom of the dialog. ILECY's `powered by` copy now uses the exact logo blue `#0066FF`, with the ILECY wordmark at `1rem`. The user-supplied ILECY logo asset is unchanged.
- Advanced the public release identity and existing graph-image watermark to 2.60 / GLASSWORK.
- The About button remains available to every signed-in role, without any access restriction.

## Implementation

- `Components/Common/CenopsAboutDialog.razor` — semantic details and responsive layout.
- `Components/Common/CenopsAboutDialog.razor.css` — all About-specific presentation; no modal-shell or app-global overrides.
- `Components/Common/CenopsRelease.cs` — single public display identity.
- `Components/Common/AppModal.razor` — optional `AccessibleLabel` for headerless modals; existing title-based behavior is unchanged.
- Existing graph export script/cache versions bumped to 2.60.

## Manual acceptance

1. Open the Skyline information button as a normal user, admin, and owner.
2. Confirm there is no generic «درباره سامانه» header and the visible name is on the left of the large cyan mark.
3. Verify the legal notices mention all product variants and the system logo.
4. Verify both the footer divider and `powered by` + unchanged blue ILECY logo + `ILECY` at 1rem.
5. Verify narrow-screen stacking, modal close/escape/backdrop, and screen-reader labeling.
6. Run `dotnet build` and a live Blazor check on a development machine (SDK not installed in this workspace).
