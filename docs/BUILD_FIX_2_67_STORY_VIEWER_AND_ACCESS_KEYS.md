# C.E.N.O.P.S. 2.67 — Build correction

This is a targeted source correction to GLASSWORK 2.67; no schema change or version bump.

## Fixed

1. **Story viewer DTO/component collision:** The application record named `ReleaseStoryViewer` conflicted with the Blazor component `ReleaseStoryViewer.razor`, causing Razor to resolve `_viewers` as `List<NTSW_CENOPS.Components.Stories.ReleaseStoryViewer>`. Renamed the application DTO to `ReleaseStoryViewerInfo` in the application contract, infrastructure story service, and Razor component. The component retains its public name and behavior.
2. **Missing namespace for access keys:** Added `using NTSW_CENOPS.Domain.Authorization;` to `Infrastructure/Files/FileResourceAccess.cs` so that `AccessKeys.Chat` resolves.

## Validation

Run `dotnet build NTSW_CENOPS.sln` with the .NET 8 SDK in the repository root. New static regression checks are at `tests/ReleaseStories267/test_release_stories_buildfix.py`. Compilation is not available in the build packaging environment; the fixes have been validated through source contracts and browser regressions.

The documentation filename deliberately doesn't match `GLASSWORK*.md`; it should **not** appear as an additional release story.
