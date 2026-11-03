# Glasswork 2.34.1 — Roslyn AddProject compilation fix

## Fixed issue

`SourceIntelligenceService.cs(297,82): error CS1739: The best overload for AddProject does not have a parameter named filePath`

## Root cause

The Roslyn `Solution.AddProject(ProjectId, name, assemblyName, language)` overload does not expose a `filePath` named argument. The previous implementation attempted to pass one directly.

## Change

The indexer now creates a Roslyn `ProjectInfo` using `ProjectInfo.Create(id, VersionStamp.Create(), name, name, LanguageNames.CSharp, filePath: path)` and passes it to `Solution.AddProject(ProjectInfo)`. The following compilation and parse options remain unchanged. This retains the registered source project's file path in the Roslyn workspace.

**Affected file:** `src/HealthMonitoringApp.Infrastructure/SourceIntelligence/SourceIntelligenceService.cs`

No other source files have been modified. No database migration or configuration change is needed.

## Installation

For the smallest update, replace only the affected C# file in the 2.34 solution. Otherwise use the full 2.34.1 project ZIP or the changed-files ZIP.

From the solution directory run:

```powershell
dotnet restore
dotnet build
```

This patch has been statically checked and archived successfully. **The .NET build has not been run in this environment**, so compilation and source-indexing behavior should be verified locally.
