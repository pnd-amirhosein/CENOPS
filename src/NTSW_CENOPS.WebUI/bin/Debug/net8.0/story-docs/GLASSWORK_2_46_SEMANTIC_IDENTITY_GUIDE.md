# Glasswork 2.46 — Source Intelligence Semantic Identity Repair

Baseline: Glasswork 2.45. Scope: semantic indexing and reverse-reference graph correctness. No database migration.

## Why this release exists

Many enterprise CQRS projects contain hundreds of methods named `Handle`. The identity of a method is **not** its name. The correct identity includes its containing type, parameter types and overload, within a particular project/compilation. A false identity match can make the graph expand unrelated handlers and report hundreds of unrelated references.

The v6 index already tracked syntactic ownership, but some lookups still depended on formatted symbol keys and accepted the *first* candidate Roslyn returned after an ambiguous binding. That is not a reliable basis for a semantic dependency graph.

## Changes

1. **Exact in-memory Roslyn resolution (`SourceSymbolIdentity`)** — indexes canonical `ISymbol` declarations using `SymbolEqualityComparer.Default`. Constructed generic references resolve to their original definitions; overloads and unrelated containing types remain distinct. Textual names are no longer used to join references or MediatR dispatches to declarations.
2. **Project-scoped persisted IDs** — stable declaration IDs include the registered source project's relative path and the fully qualified declaration signature. Local/parameter/type-parameter declarations include their source locations. A pair of separate projects with an identical assembly/type name cannot silently share a persisted ID. Duplicate project filenames are given unique *synthetic* Roslyn assembly names (the displayed project names do not change).
3. **Reject ambiguous results** — `GetSymbolInfo(...).CandidateSymbols.FirstOrDefault()` is removed. When no unique semantic `Symbol` exists, indexing **does not guess**. Such ambiguous binding sites are counted as index diagnostics; they do not produce a false graph edge.
4. **Precise MediatR dispatch** — MediatR request types are compared with Roslyn semantic equality. When `IRequestHandler<TRequest,TResponse>` is resolved, the indexer verifies the concrete method actually implements the interface's `Handle`, rather than accepting another method with the same name. `Send` and `Publish` edges remain explicitly *inferred* and still require evidence of an appropriate MediatR receiver and a concrete indexed request type. Multiple possible `Send` handlers are not arbitrarily disambiguated.
5. **Index v7** — old snapshots remain readable for source search and Tree view, but graph expansion requires a fresh v7 index. This avoids continuing exploration with legacy symbol IDs. The UI exposes the count of skipped ambiguous bindings, clearly labeled as **index-wide**, not missing references to the selected symbol.
6. **No change to the graph presentation or business permissions** — source previews and project-level permission guards are unchanged. `+` continues to request only direct consumers of the **exact** selected vertex's symbol ID.

## Files changed

- `src/HealthMonitoringApp.Infrastructure/SourceIntelligence/SourceIntelligenceService.cs`
- `src/HealthMonitoringApp.Infrastructure/SourceIntelligence/MediatRDispatchIndexer.cs`
- `src/HealthMonitoringApp.Infrastructure/SourceIntelligence/SourceSymbolIdentity.cs` (new)
- `src/HealthMonitoringApp.Application/SourceIntelligence/SourceModels.cs`
- `src/HealthMonitoringApp.WebUI/Components/Pages/SourceExplorerPage.razor`
- `tests/SourceIntelligenceSemanticIdentityRegression/SourceIntelligenceSemanticIdentityRegression.csproj` (new)
- `tests/SourceIntelligenceSemanticIdentityRegression/Program.cs` (new)

## Deployment and configuration

No changes to `SourceIntelligence:RootPath`, `IndexPath`, or other appsettings keys. No SQL migration and no new NuGet dependencies beyond the existing Roslyn package used by the application. The standalone regression runner references that same Roslyn Workspaces version.

1. Back up the application and the `SourceIndexes` directory.
2. Replace the updated project files.
3. Run `dotnet restore` and `dotnet build` from the solution root.
4. Run `dotnet run --project tests/SourceIntelligenceOwnershipRegression/SourceIntelligenceOwnershipRegression.csproj`.
5. Run `dotnet run --project tests/SourceIntelligenceSemanticIdentityRegression/SourceIntelligenceSemanticIdentityRegression.csproj`.
6. Open **ابزارها → مدیریت منابع** and run **بازایندکس** on every source repository used for graph exploration. Existing v6 index JSON cannot supply v7 exact-symbol identities.
7. In **ابزارها → تحلیل سورس**, search `CheckSaveInspectionCertificateBillOfLading`. Compare the Tree's actual locations to Rider. Open Graph and expand each real consumer declaration independently.

## Acceptance checks with real source

- Tree reports the five source occurrences from Rider (two `new` dispatches, handler interface, handler parameter, and unit test), **if all are in the registered indexing scope** and Roslyn resolves them. The graph may show fewer nodes when multiple occurrences share one real enclosing declaration; every grouped node must reveal the correct class/method signature and file.
- Expanding `InspectionCertificateOverviewValidationQueryHandler.Handle(InspectionCertificateOverviewValidationQuery, CancellationToken)` must resolve references to **that exact method**, not all methods named `Handle`.
- A different `IRequestHandler` with another request type must **not** appear merely because its method is called `Handle`.
- For MediatR, dispatch links are inferred from request types and actual `Send`/`Publish` sites. If source dependencies are missing, the analyzer must leave the edge unresolved rather than infer it from similar names.
- Reopen CENOPS after indexing and verify that the v7 index loads and has identical IDs/counts. (The index version must be 7.)
- Graph expansion must be unavailable on an older index until reindexed; Tree locations stay accessible.
- Test both projects that intentionally share an assembly/project name and overloaded method names to ensure no false merging.

## Honest limitations

This is an **accuracy repair**, not a full MSBuild-based semantic compiler or a promise of 100% Rider parity. The existing indexer creates a synthetic Roslyn workspace without executing imported MSBuild targets or restoring NuGet packages. Generated code, conditional compilation, reflection, dependency injection, external assemblies, and unresolved project references can prevent accurate static reference detection. The new rule is **unknown rather than wrong**. The regression project is source-provided, but a real .NET build/run in your own environment is required to validate compilation and integration.

For the next diagnostic cycle, if a reference is missing, capture the registered `.sln/.slnx/.csproj` scope, the exact target signature, the Tree locations, and any index-wide ambiguity/truncation warning. That distinguishes missing input from semantic resolution errors without guessing.
