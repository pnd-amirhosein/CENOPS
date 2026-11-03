# Glasswork 2.36 — Exact Declaration Resolver

**Baseline:** NTSW_CENOPS_Glasswork_2_35  
**Scope:** Source Intelligence Phase 1 — declaration search only  
**Database migration:** None  
**Configuration changes:** None

## Why

The old search matched *substrings* of symbol names (`Contains`, case-insensitive). Searching `BillOfLading` returned declarations such as `GetBillOfLading`, and the screen mixed definitions with call/reference exploration. It was difficult to identify where a named class, property, or variable was actually **declared**.

This release gives the search screen one responsibility: resolve the exact, case-sensitive name of a declared C# symbol, then inspect **that declaration**, without traversing usages.

## Search contract

- Queries are trimmed before matching.
- Names are compared using `StringComparison.Ordinal` (C# case-sensitive semantics).
- Only **declaration names** are matched; namespaces, signatures, parameter types, usages, calls, and references do not contribute to a hit.
- `BillOfLading` matches `class BillOfLading`, a property *named* `BillOfLading`, variables and parameters named `BillOfLading`, etc. It does **not** match `GetBillOfLading`, `BillOfLadingService`, or `billOfLading`.
- Two declarations with the same simple name in different projects, scopes, or namespaces remain separate results. Overloads remain separate. Partial declarations are deduplicated according to Roslyn's logical symbol identity.
- A constructor's displayed search name is its declared containing type name rather than Roslyn's internal `.ctor` name.
- All results are counted before paging. Categories have full counts even when a name has hundreds of declarations. The default page size is 80.
- Search is restricted to the CENOPS user permission `tools.source-intelligence`.

### Groups

| Category | Symbol kinds |
| --- | --- |
| Types | Class, record, record struct, interface, enum, struct, delegate |
| Methods | Methods and constructors |
| Properties | Properties (including DbSet properties) |
| Fields & events | Fields, C# events, enum members |
| Local variables | Locals, foreach/catch/deconstruction/pattern variables |
| Parameters | Method and type parameters |
| Other | Remaining declarations, e.g. namespaces |

**Entity** is a domain concept, not a distinct C# Roslyn symbol kind. An entity is typically indexed as `Class` or `Record`; `DbSet<TEntity> Foo` is a `Property` named `Foo`.

## Source inspection behavior

- Selecting a hit retrieves `GetDeclarationAsync`, which returns the indexed definition only, not incoming/outgoing relationships.
- Inspector shows the symbol's actual name, kind, signature, containing symbol (if available), project, relative source path, line/column, namespace and a bounded source excerpt.
- The existing reference/graph service contracts remain in place for later phases, but they are not invoked by this screen.
- Both the source-registration workflow and manually triggered indexing remain unchanged.

## Index changes and compatibility

Added declaration coverage for `SingleVariableDesignationSyntax`, `ForEachStatementSyntax`, `CatchDeclarationSyntax`, generic type parameters and namespace declarations. Records and enum members have clearer kinds; constructors use their C# source name. Newly indexed symbols include an optional containing-symbol display string.

The index snapshot version is now **3**. Old JSON snapshots remain readable, but **reindex each registered source once** after deploying 2.36 to get the expanded declaration set and containing-symbol information. Old indexes still support exact searches for the declarations already captured by 2.35.

Roslyn operates in the existing synthetic, no-build workspace. No imported solution is restored, built or executed. Unresolved dependencies or conditional compilation can still limit what semantic declarations Roslyn discovers. This update does not change that architecture.

## Changed files

1. `src/HealthMonitoringApp.Application/SourceIntelligence/SourceModels.cs` — new declaration search paging contracts and read-only declaration lookup.
2. `src/HealthMonitoringApp.Application/SourceIntelligence/SourceDeclarationCategories.cs` — common, reusable declaration-kind grouping rules.
3. `src/HealthMonitoringApp.Infrastructure/SourceIntelligence/SourceIntelligenceService.cs` — ordinal exact resolver, category totals/paging, declaration-only retrieval, additional declaration syntax support.
4. `src/HealthMonitoringApp.WebUI/Components/Pages/SourceExplorerPage.razor` — focused resolver UI, filters and definition inspector; reference tree removed from the view.
5. `src/HealthMonitoringApp.WebUI/Components/Pages/SourceExplorerPage.razor.css` — updated responsive result/inspector styling and removal of unused reference-browser styling.

## Deployment

1. Back up the running installation and use the full ZIP or changed-files ZIP to update the project.
2. Keep `SourceIntelligence:RootPath` and `SourceIntelligence:IndexPath` unchanged.
3. Run `dotnet restore` and `dotnet build` on the target machine.
4. Open **ابزارها → تحلیل سورس** and reindex registered sources once.
5. Search a declaration name and inspect the results. No database migration is needed.

## Local acceptance checklist

- [ ] Searching `BillOfLading` returns `class BillOfLading`, a `BillOfLading` property and variable if they exist.
- [ ] It does not return `GetBillOfLading`, `BillOfLadingService`, or `billOfLading`.
- [ ] A type reference inside a method signature without an identically named declaration is not returned.
- [ ] Search for a repeated local variable name; its results appear under **متغیرهای محلی**, including separate scopes.
- [ ] Search for a constructor, enum member, method overload, record and generic type parameter.
- [ ] Category chips show complete counts. Change categories and pages; count and results remain consistent.
- [ ] Clicking results updates the definition inspector only; no reference tree appears.
- [ ] Switching between registered sources changes the search scope as requested.
- [ ] Existing registered sources, snapshots and configuration continue working after the update; reindex adds new symbols.
- [ ] Run on a large solution and watch memory/indexing performance.

## Verification notes

Source-level assertions, Razor structural checks, CSS parsing, project XML parsing, ZIP archive CRC checks, and baseline-versus-changed-file comparison are possible in the packaging environment. **A real `dotnet build` and live Roslyn analysis were not run here**, because the .NET SDK is unavailable. Verify them on your Windows/.NET development environment before deploying.
