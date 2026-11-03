# Glasswork 2.37 — Source Intelligence: One-hop references

Based on **Glasswork 2.36**. No new database migration or new NuGet dependency.

## What's new

### 1. Source management and exploration are separate

- **ابزارها → مدیریت منابع** (`/tools/source-indexes`): discover server folders and `.sln` / `.slnx` / `.csproj` files, register a source, manually index/reindex, inspect indexing status/errors. Refresh folders after manually copying new source.
- **ابزارها → تحلیل سورس** (`/tools/source-explorer`): exact, case-sensitive **declaration-name** search with the existing declaration categories, symbol inspector, and direct references grouped by project/file.
- Both pages use the **existing** `AccessKeys.SourceIntelligence` authorization. The management and search services independently enforce access; splitting the UI has not weakened server-side permission checks.
- The skyline Tools → تحلیل سورس shortcut continues to open the explorer.

### 2. Single-layer source references

1. Search for a declaration's **exact name** (`BillOfLading` != `GetBillOfLading` or `billOfLading`). The initial search remains definitions-only, with category counts and pagination.
2. Select a **specific declaration**. Its name, signature, and source remain pinned.
3. The inspector loads **direct occurrences** identified by Roslyn for that symbol, plus directly resolved interface implementations, overrides, and inheritance declaration locations.
4. Expand an accordion for **Project → File**. Each occurrence shows its containing member, line/column, and short source excerpt.
5. Click an occurrence to open a **read-only snippet modal** showing surrounding lines. Close with the standard modal ESC control or backdrop. The selected declaration does **not** change.

We deliberately do **not** traverse the callers of callers or recursively expand related symbols. The search result count is a count of exact declarations, not reference occurrences. Usage counts are separately shown after selecting a declaration.

### 3. Reference data and Roslyn

- Added `SourceReferenceResult` as a focused application-layer contract and `ISourceIntelligenceService.GetReferencesAsync(...)`, independent of the older graph-oriented `GetSymbolAsync` API.
- Direct occurrence data comes from previously persisted `SourceUsage` records, including project, file, containing member, exact line/column, and ± source-code lines.
- The service deduplicates same-location references and also surfaces direct `Implements`, `Overrides`, and `Inherits` relationships at their implementing declaration locations.
- All source excerpts are drawn from the **indexed snapshot**, not from live filesystem reads when someone clicks. The viewer never executes analyzed source.
- **Fixed a critical compatibility bug:** index creation writes `SourceIndex.Version = 3` while the loader previously accepted only v1/v2. The loader now accepts all three, including after application restart.
- Older indices remain readable, but v1/v2 lack complete position-based usage details; **reindex** each registered source after upgrading when its index predates 2.35.

**Known semantic limits:** This first-phase Roslyn index uses an isolated synthetic workspace; no restore/build is executed. Unresolved packages, generated source, dynamically constructed names, reflection, DI registrations discovered only through runtime behavior, and implicit expressions can cause incomplete results. Also, `MaxRelations` limits stored usages across an entire indexed source, not individually per symbol; the UI warns of possible truncation. We can improve semantic completeness in a later analyzer-focused phase.

### 4. Persian text in code snippets

- Added reusable read-only `SourceCodePreview` with a dark, scrollable, escaped code surface.
- Adjusted mixed Persian/Latin bidirectional text handling (`unicode-bidi: plaintext`) for source preview and shared NoteSnippetBox's editing/highlight layers.
- The same rules apply to both textarea and highlighting overlay to avoid misaligning cursor/text.
- All code excerpts are rendered as **text**, never as raw HTML; indexed source cannot inject markup into the inspector.

## Configure and use

The same configuration continues to work:

```json
{
  "SourceIntelligence": {
    "RootPath": "D:\\CENOPS\\Sources",
    "IndexPath": "D:\\CENOPS\\SourceIndexes",
    "MaxFiles": 20000,
    "MaxFileBytes": 4000000,
    "MaxTotalBytes": 500000000,
    "MaxRelations": 500000
  }
}
```

`MaxFiles` and other sizing limits are environment-dependent safety controls. The `RootPath` and `IndexPath` must be separate, absolute paths outside the site's public web root. They can be Windows or Linux locations. The index directory must be writable by the application service identity.

**To index:** copy source into `RootPath/<registered-subdirectory>`; navigate to **ابزارها → مدیریت منابع**; refresh folders, choose solution/project, register, and index. Existing registered sources can be reindexed from this page.

**To explore:** navigate to **ابزارها → تحلیل سورس**; choose a repository (or all); search exact declaration name; pick one of the results; expand relevant files and click a usage.

No Git integration is included. Git will later update the same source directory and call the existing index process.

## Security and maintainability

- Source-index and source-search calls both check `tools.source-intelligence` on the server.
- No changed database schema; no source code is placed under `wwwroot`.
- No imported MSBuild targets, restore, or source programs are executed.
- Do not expose source-root directories as static web files. Treat persisted JSON indexes as sensitive because they contain source excerpts.
- Source windows can contain secrets from analyzed repositories; restrict server and index permissions accordingly.

## Local test checklist

- [ ] `dotnet restore` and `dotnet build` the full solution (not available in the packaging environment).
- [ ] Open `/tools/source-indexes`; register another manually copied solution and reindex it.
- [ ] Restart CENOPS and confirm a version-3 indexed source still appears and is searchable.
- [ ] Search `BillOfLading`; verify exact names only; select the class or property separately and confirm they have distinct references.
- [ ] Confirm references expand by **project and file**, not caller/callee chains.
- [ ] Click a usage; inspect the highlighted line and surrounding lines; close the modal with Escape and confirm the original symbol is still selected.
- [ ] Compare counts and usage locations against **Find All References** in Visual Studio/Rider. Note that unavailable dependencies may reduce coverage.
- [ ] Try Arabic/Persian text inside code comments and strings (e.g. `// نمایش داده‌ها`); verify it reads correctly, with code indentation preserved.
- [ ] Test the existing NoteSnippetBox with mixed RTL/LTR text; confirm its text layer still aligns with the caret.
- [ ] Sign in as a user **without** Source Intelligence access; both pages and direct service calls must deny access.
- [ ] Set `MaxRelations` low on a test source and confirm the UI warns when the index may be truncated.

**Verification done here:** static source assertions, CSS parse validation, project XML parse validation, archive integrity and source-to-package comparisons. A full .NET build, Roslyn index execution, and UI integration tests **were not run** because .NET SDK is not installed in the environment.
