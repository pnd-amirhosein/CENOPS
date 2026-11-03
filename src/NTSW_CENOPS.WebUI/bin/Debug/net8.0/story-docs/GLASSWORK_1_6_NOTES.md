# Glasswork 1.6 — Unified loading and notifications

## Usage

**Loader component**: `<AppLoading IsLoading="@isBusy" Variant="overlay" Label="در حال دریافت اطلاعات..." />`; choose `page`, `overlay` (within a positioned parent), or `inline`. It renders the original animated `/gif/loading.gif` with a translucent/blurred background.

**Loading service**: Inject `AppBusyService` and acquire an independent disposable lease around async work:

```csharp
using var loading = Busy.Begin("در حال دریافت داده‌ها...");
await MyService.GetDataAsync();
```

The scoped `AppLoadingHost` in `MainLayout` is the single fullscreen overlay; overlapping operations cannot hide each other prematurely. Route navigation and Identity fetches/role assignment use the service. The DataTable uses the same `AppLoading` locally for its own CRUD/refresh waits.

**Once-per-login intro**: On entering the authenticated shell, `OnAfterRenderAsync` consults a per-tab sessionStorage marker (reset when returning to the login/signup/password forms) and retains the glass loading transition for 500 milliseconds. Refresh/normal navigation do not re-run it.

**Notices**: `AppNoticeService.Success("...", duration?)` / `.Error("...", duration?)` emit reusable `AppNoticeMessage` via `AppNoticeHost`, styled like the Blazor reconnection notice. Success disappears after 4s, errors after 7s, and both can be dismissed manually. Identity operations now report both successful and rejected results; the DataTable stays in edit mode on failure. Profile update POST redirects convey their result to the same notice service. Ordinary successful GETs are intentionally silent to avoid notification spam; failed GETs show an error.

**Rendering**: `Routes` and `HeadOutlet` use a shared `InteractiveServer` render mode for the authenticated shell (required so the layout and page services share one circuit). HTTP auth-form routes (`/`, `/signup`, `/change-password`) remain static/posted to the existing endpoints.

No Identity/domain/schema migration was added.
