# Glasswork 2.35 — Source Intelligence: Definition-First Explorer

Baseline: **Glasswork 2.34.1**. This update improves the first phase of Source Intelligence; Phase 2 diagrams and Phase 3 Git syncing remain separate future work.

## Why the original search felt confusing

Version 2.34.1 indexed only a subset of declarations (e.g., classes, methods and properties) and searched **names, signatures and namespaces**. A search for `Declaration` therefore returned methods whose signatures happened to mention `Declaration`, even if those methods were not named that way. Its two relationship lists also navigated away from the selected symbol when clicked.

Version 2.35 separates three concepts:

- **Declaration / تعریف:** the location that defines a symbol. C# classes, interfaces, structs, enums, properties, methods, fields, local variables, parameters and several additional declaration kinds are indexed.
- **Reference / محل استفاده:** an exact statically resolved occurrence of the chosen declaration in code. A field initializer or `DbSet<Declaration>` can be a reference to a `Declaration` type even though the containing property is not named `Declaration`.
- **Caller, callee, implementation:** special semantic relationships. The index still retains its original relationship graph for Phase 2; the Phase 1 inspector now prioritizes exact source occurrences.

For example, searching `Declaration` may find the `Declaration` class itself and a property named `Declarations`. Selecting the class can reveal references in `AppDbContext` and `IAppDbContext`, where `DbSet<Declaration>` appears. Whether those references resolve depends on the available source and Roslyn's compilation model.

## New explorer behavior

1. Search matches **a declared symbol's own name**, not incidental matches in its parameter/return types or namespace.
2. Results are grouped by kind (class, property, field, method, variable, parameter, etc.), with an exact-name-first ranking.
3. Selecting a result pins its declaration and source excerpt in the inspector.
4. A collapsible usage tree groups occurrence locations by **project → folder → file → containing member → line**.
5. Clicking a usage displays its indexed source context with line numbers, without changing the symbol being investigated.
6. Occurrence records include the precise line and column, a source line, the containing member and a bounded code window. These are stored with the index snapshot so the preview matches the revision that was indexed.
7. A warning appears if reference results may be incomplete because the snapshot is old or `MaxRelations` was reached.

Calls/implementations remain relationship types inside the index; they are no longer presented as two ambiguous navigation lists in this screen. The Phase 2 diagram viewer can use them later.

## Register a specific solution or project

Navigate to **ابزارها → تحلیل سورس → ثبت سورس**:

1. Copy the complete source tree into a direct child of your configured `SourceIntelligence:RootPath` (for example `D:\CENOPS\Sources\NTSW.BusinessLogic`).
2. Select that folder. CENOPS enumerates `.sln`, `.slnx` and `.csproj` entries below it while skipping excluded and linked directories.
3. Choose a solution/project from **فایل راهکار / پروژه**, or leave **تمام فایل‌های C# پوشه** to index the entire directory as before.
4. The editable **نام نمایشی** field receives a suggested name from the chosen file. It also offers the discovered names as suggestions.
5. Register and run **ایندکس**. Registered source scope is stored as a relative path, never a client-supplied absolute filesystem path.

Scope behavior:

- `.sln` / `.slnx`: index projects whose relative paths are listed in the solution and exist within the permitted root.
- `.csproj`: index that project and any in-root, discovered project-reference dependencies.
- Blank: index all discovered C# source in the folder (legacy behavior).

The parser **never executes** `.sln`, `.csproj`, MSBuild, restore targets, or generated build scripts. It only reads and parses their text/XML. An XML external entity/DTD is not permitted.

## Source root and cross-platform configuration

**No changes are required** if 2.34 is already configured. Example Windows configuration:

```json
{
  "SourceIntelligence": {
    "RootPath": "D:\\CENOPS\\Sources",
    "IndexPath": "D:\\CENOPS\\SourceIndexes",
    "MaxFiles": 20000,
    "MaxFileBytes": 4000000,
    "MaxTotalBytes": 300000000,
    "MaxRelations": 300000
  }
}
```

These are illustrative capacities only. Large projects increase time, memory and on-disk index size. Indexing references is now more expensive because it saves bounded code contexts. Assess limits and server capacity before increasing them.

Linux uses the same keys with absolute paths such as `/var/lib/cenops/sources` and `/var/lib/cenops/index`. Source folders should remain outside the published web root and be read-only for the service identity. The index directory needs write permissions and must be protected as proprietary source material.

## Upgrading from Glasswork 2.34.1

1. Replace the changed application/infrastructure/Razor files, or deploy the full project.
2. Build normally:

   ```bash
   dotnet restore
   dotnet build
   ```

3. Start CENOPS and go to **ابزارها → تحلیل سورس**.
4. For every **existing registered source**, click **بازایندکس** once. Its old JSON index is readable but contains no precise occurrence snapshots. The new snapshot format is **version 2**.
5. Search `Declaration`, select the class, expand project/folder/file/member nodes, and click a reference to inspect the code.
6. Test another search for a local variable, a field, and an overloaded method.
7. Optionally register a separate `.sln`, `.slnx` or `.csproj` scope from the same copied folder. Existing whole-folder registrations remain intact.

**No EF database migration** is required. The registry and source index remain persisted as JSON in `IndexPath`. Registration adds an optional `ProjectFile` field that defaults to `null` when older registry records are read.

## Implementation and development notes

Changed paths:

- `src/HealthMonitoringApp.Application/SourceIntelligence/SourceModels.cs`: extended contracts for registration scopes and exact usage locations.
- `src/HealthMonitoringApp.Infrastructure/SourceIntelligence/SourceIntelligenceService.cs`: declaration-aware indexing, name-only search, occurrence snapshots, safe solution-file discovery, and scoped indexing.
- `src/HealthMonitoringApp.WebUI/Components/Pages/SourceExplorerPage.razor`: scoped registration control, grouped definitions, expandable usage tree, and independent code preview.
- `src/HealthMonitoringApp.WebUI/Components/Pages/SourceExplorerPage.razor.css`: responsive tree and code preview styling.

The index remains bounded by `MaxRelations`. A source-reference location that Roslyn cannot resolve because of missing packages, conditional compilation, generated code or dynamic dispatch cannot reliably be linked to a symbol. This remains a **best-effort semantic index**, not a fully built workspace or a guarantee of runtime call paths. Interfaces, dependency injection, reflection and polymorphic dispatch also require care.

Existing symbol relationships (`Calls`, `References`, `Implements`, `Inherits`, `Overrides`) are still stored for later diagrams. Exact occurrences are now a separate collection; this prevents loss of usages from field initializers or declarations that cannot be attributed to a calling method.

## Local test checklist

- Search a method whose parameter type includes `Declaration` but whose **name does not**: the method should not appear just because its signature contains the word.
- Search an exact class and verify class/property/field/local name matches are grouped, with exact matches first.
- In `DbSet<Declaration>`, verify clicking the reference shows that line and surrounding lines, while the `Declaration` class stays selected.
- Select two different overloads and verify references distinguish them when Roslyn can resolve calls.
- Test a property reference inside a field initializer (no caller method), and variable references within a method.
- Test duplicate symbol IDs across two registered source folders; search selections should not collide.
- Test `.sln`, `.slnx`, `.csproj` registration in folders with nested projects, alongside a blank whole-folder scope.
- Ensure disallowed paths, `..` traversal, linked/reparse-point directories and unauthorized users cannot access source content.
- Reindex an existing 2.34 index and verify the new references appear; confirm old snapshots remain loadable until reindex.
- Exercise a large source exceeding `MaxRelations` and confirm the warning rather than an implied guarantee of complete references.
- Verify both Windows and Linux path behavior when deployed on those systems.

**Verification performed in this environment:** inspected source/API wiring, configuration contracts and changed paths, validated project XML and CSS syntax, checked ZIP integrity and exact changed-file contents. **Not performed:** real .NET/Razor compilation, MSBuild/Roslyn runtime reference analysis, or database-backed Blazor tests. Those need your local SDK and registered solution.
