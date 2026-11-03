# Glasswork 2.46.1 — Source Intelligence duplicate symbol IDs

**Base:** Glasswork 2.46  
**Scope:** Roslyn persisted IDs, safe legacy index loading, graph readiness, regression coverage.  
**Database migration:** None  
**Source reindex:** Required for full graph exploration.

## Problem

After indexing or selecting references, the application could throw:

```text
An item with the same key has already been added. Key: BEB9AEA636EDDF1A92B7D209
```

CENOPS uses the first 24 hex characters of a SHA-256 digest as a persisted symbol ID. In 2.46 the input key was composed mostly of the project path and `SymbolDisplayFormat.FullyQualifiedFormat` text. Two semantically distinct Roslyn declarations can have the same display-formatted key. Once both were written into `SourceIndex.Symbols`, `ToDictionary(symbol => symbol.Id)` threw when opening references. The duplicate is not evidence of a cryptographic SHA-256 collision; it is a **non-unique input key** problem.

## Changes

1. **Collision-resistant persisted IDs:** `SourceSymbolIdentity.PersistentDeclarationKey` now appends a deterministic source location (source path and span offset) for *every* source declaration, not just locals, parameters, and type parameters. It normalizes paths relative to the configured root during indexing. Roslyn semantic equality continues to control reference matching; persisted keys are only for storage and lookups.
2. **Pre-publish uniqueness guard:** registering two different semantic declarations with one ID is rejected before a corrupt index can be published. Repeated/partial declarations of the *same* canonical Roslyn symbol remain deduplicated.
3. **Safe legacy lookups:** existing version-7 indexes that already contain duplicate IDs no longer trigger `ToDictionary()` exceptions. Lookup maps deliberately exclude ambiguous IDs rather than silently selecting an arbitrary symbol. Their Tree occurrences can still be inspected, but the Graph requires a new verified snapshot.
4. **Index format v8:** Graph identity verification requires v8. Versions 1–7 can still be read for compatible browsing, but won't be trusted for semantic multi-hop expansion.
5. **Regression coverage:** the semantic-identity regression suite checks two same-named declarations from different C# source files, as well as the duplicate-ID registration guard. The previous overload, generic, MediatR, and ambiguity checks remain intact.

## Changed files

- `src/HealthMonitoringApp.Infrastructure/SourceIntelligence/SourceSymbolIdentity.cs`
- `src/HealthMonitoringApp.Infrastructure/SourceIntelligence/SourceIntelligenceService.cs`
- `src/HealthMonitoringApp.WebUI/Components/Pages/SourceExplorerPage.razor`
- `tests/SourceIntelligenceSemanticIdentityRegression/Program.cs`
- This guide

## Upgrade and test

1. Back up the existing index files in the configured `SourceIntelligence:IndexPath`. No database migration or change to `RootPath`/`IndexPath` is needed.
2. Replace the project or the changed files, then rebuild and restart CENOPS:

   ```powershell
   dotnet restore
   dotnet build
   dotnet run --project tests/SourceIntelligenceSemanticIdentityRegression/SourceIntelligenceSemanticIdentityRegression.csproj
   dotnet run --project tests/SourceIntelligenceOwnershipRegression/SourceIntelligenceOwnershipRegression.csproj
   ```

3. Open **ابزارها → مدیریت منابع** and run **بازایندکس** for the affected repository. The regenerated index must be v8.
4. In Source Explorer, search `CheckSaveInspectionCertificateBillOfLading` and inspect the Tree and Graph. Verify that unrelated `Handle` methods do not reappear and that expanding a method only follows references to that precise symbol.
5. If indexing still reports a collision, capture the full exception stack and the first conflicting declarations (avoid sending proprietary code or credentials). A remaining collision is now an explicit index-generation failure, not an silently corrupted graph.

## Verification boundaries

- The original ZIP was inspected; updated C# source and index-version contracts were statically checked.
- Both delivered ZIPs passed file-integrity validation.
- **Not executed here:** `dotnet build`, C# regression suite, and real-repository Roslyn/Blazor integration (the .NET SDK is unavailable in this environment).

## Design note

This patch corrects persisted **identity collisions**, not all missing reference paths. The synthetic Roslyn workspace can still produce incomplete references for unresolved NuGet dependencies, generated code, or runtime framework dispatch. CENOPS should continue to label inferred MediatR relationships separately from direct semantic usages.
