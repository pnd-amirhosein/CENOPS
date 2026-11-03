# Glasswork 2.34 — Source Intelligence (Phase 1)

This release adds a **manual-folder-based .NET source explorer** to CENOPS. It intentionally does **not** contain the Phase 2 diagram renderer or Phase 3 Git synchronization agent.

## How to get running on Windows

### 1. Choose two directories outside the website

Example:

```text
D:\CENOPS\Sources\                   <-- imported solutions, read-only for CENOPS
    NTSW.BusinessLogic\
        BusinessLogic.sln
        src\...
D:\CENOPS\SourceIndexes\             <-- generated registry and JSON indexes, writable
```

Do not use `wwwroot`, the deployed application's content root, a public network share, or a Git checkout that the web app can modify. Give the IIS application-pool identity **read** access to `Sources` and **read/write** access to `SourceIndexes`. Restrict both folders to trusted administrators and the application identity; no public IIS static-file mapping should expose them.

### 2. Configure CENOPS

Set these values in `appsettings.Production.json` (or with environment variables in production):

```json
{
  "SourceIntelligence": {
    "RootPath": "D:\\CENOPS\\Sources",
    "IndexPath": "D:\\CENOPS\\SourceIndexes",
    "MaxFiles": 3000,
    "MaxFileBytes": 2000000,
    "MaxTotalBytes": 60000000,
    "MaxRelations": 150000
  }
}
```

Recommended environment-variable alternative:

```text
SourceIntelligence__RootPath=D:\CENOPS\Sources
SourceIntelligence__IndexPath=D:\CENOPS\SourceIndexes
```

Restart CENOPS after updating settings.

On Linux, use the identical keys with paths such as `/var/lib/cenops/sources` and `/var/lib/cenops/source-index`; grant the service identity the appropriate Unix permissions. The indexer contains no Windows-only file operations.

### 3. Apply the access-permission migration

From the solution root:

```powershell
dotnet restore
dotnet build
dotnet ef database update --project src/HealthMonitoringApp.Infrastructure --startup-project src/HealthMonitoringApp.WebUI
```

The migration `20261018090000_AddSourceIntelligenceAccess` creates permission `tools.source-intelligence` and grants it to existing **Admin** and **Owner** roles. New Admin/Owner roles created through the role seeder receive this permission too. Other roles can be explicitly granted the permission using the existing CENOPS Access Management UI. Every service operation independently checks the caller's **live database access**, not only the menu/page state.

### 4. Copy and index a .NET solution

1. Copy the entire source tree under **one folder directly beneath** `RootPath`, for example `D:\CENOPS\Sources\NTSW.BusinessLogic`.
2. Open **ابزارها → تحلیل سورس** or the tools menu in Skyline, then choose **تحلیل سورس (S)**.
3. Select the newly copied folder and enter a display name.
4. Click **ثبت پروژه** to register it.
5. Click **ایندکس** next to the repository. Large solutions can take time.
6. Search for a method, class, constructor, interface or signature; click a result to see source text and incoming/outgoing relationships.
7. To update manually, copy newer files into the **same registered directory** and click **بازایندکس**. The old index remains available if reindexing fails.

The repository's SHA-256 content fingerprint, file count, symbol count, relationship count, and last indexing timestamp are shown/stored. The index registry and snapshots are durable JSON files under `IndexPath`; **no database table** is used to store source code or symbols. All paths inside the index are **relative** to the registered root.

## Features available in Phase 1

- Register multiple folder-based .NET solutions (no Git credentials required).
- Search project/class/interface/record/method/constructor/property/event declarations by names, namespaces and signatures.
- Inspect a bounded excerpt of the exact source indexed, with its project, relative filename and line/column.
- Inspect `Calls`, `References`, `Implements`, `Inherits`, and `Overrides` relationships where Roslyn can resolve them.
- Reindex manually without adding duplicates to the repository registry.
- Built-in limits for source file size, total bytes, number of files, and relationship count.
- Hide source explorer from users without `tools.source-intelligence` and deny unauthorized service calls.

## Deliberate architecture trade-off

The importer builds a **synthetic Roslyn workspace**, inspecting files and `ProjectReference` entries without executing MSBuild, `dotnet restore`, generators or imported code. This is safer for manually copied repositories. It means semantic resolution is **best-effort**, not a promise of exact runtime call graphs. External packages, conditional compilation, generated source, reflection, dynamic dispatch and dependency injection may be unresolved. `References` represent statically resolved symbol references; `Calls` represent statically resolved invocation targets. Each class member is identified using a stable hash of its assembly and fully qualified signature.

In Phase 2, the saved relationship model will feed an interactive diagram viewer. In Phase 3, a Git synchronizer can update the same source directories and call the existing `IndexAsync` method; the Roslyn engine does not need to know about Git.

## Operational and security notes

- Treat the index as **sensitive source code**. The JSON index contains source excerpts and must never be served as static files.
- Register only repositories you trust. XML document type processing is disabled for project files; all linked/reparse-point files and directories encountered while traversing the source are skipped.
- Only **direct child directories** of the configured source root may be registered. Arbitrary paths and `..` traversal are not allowed.
- Indexing runs off the Blazor UI thread, is serialized across indexing requests, and publishes a snapshot by atomic replacement. CENOPS does not write back to the imported source tree.
- Limit file and relation counts to match server capacity. Consider running indexers as a separate limited-privilege service in Phase 2 if repositories are large or untrusted.
- The importer is **not a build system** and cannot guarantee complete references for repositories requiring external dependencies or custom MSBuild evaluation.

## Files changed / added

- `Application/SourceIntelligence/SourceModels.cs`: contracts and indexing/search API.
- `Infrastructure/SourceIntelligence/SourceIntelligenceService.cs`: bounded Roslyn analysis and snapshot storage.
- `Infrastructure/HealthMonitoringApp.Infrastructure.csproj`: adds `Microsoft.CodeAnalysis.CSharp.Workspaces` 4.11.0.
- `Infrastructure/DependencyInjection.cs`: registers the service and options.
- `Infrastructure/Migrations/20261018090000_AddSourceIntelligenceAccess.cs`: access migration.
- `Domain/Authorization/AccessKeys.cs`, `Infrastructure/Authorization/AccessService.cs`, `DefaultRoleAccessSeeder.cs`: permission integration.
- `WebUI/Components/Pages/SourceExplorerPage.razor` and `.razor.css`: registration/search/detail UI.
- `WebUI/Components/Layout/NavMenu.razor`, `Skyline.razor`, `MainLayout.razor`: links and route titles.
- `WebUI/appsettings.json`: configurable root, index and resource limits.

## Local verification checklist

1. `dotnet restore`, `dotnet build`, and EF migration complete successfully.
2. A User without the new permission cannot open the page, view the menu item, or query the service.
3. An Admin can register one direct-child folder; path traversal or a symlink is rejected.
4. Index a small test solution containing a class implementing an interface, another method calling it, and an override. Verify searches and relationships.
5. Change one method, manually reindex, and confirm that the fingerprint and search results update.
6. Restart the application and confirm index/catalog survive the restart.
7. Try a source tree with `bin`, `obj`, and `.git` directories; those should not be indexed.
8. Try an incomplete solution with missing NuGet packages; confirm that any missing semantic edges are treated as limitations, not invented results.
9. Confirm `wwwroot` and browser network requests cannot fetch source or index JSON directly.
10. Test a Linux-hosted installation using the same configuration keys with Unix paths.

**Verification performed for this package:** Source inspections, configuration/route wiring checks, project XML parsing, zip integrity, and changed-file comparison. **Not performed in this environment:** `.NET`/Razor compilation, NuGet restore, SQL migration execution, or live Roslyn indexing (the .NET SDK is not installed here). These must be verified locally before production deployment.
