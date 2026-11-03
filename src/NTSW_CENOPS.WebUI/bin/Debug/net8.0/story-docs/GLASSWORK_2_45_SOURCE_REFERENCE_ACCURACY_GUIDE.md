# Glasswork 2.45 — Source Intelligence Reference Accuracy

## Scope

Built from Glasswork 2.44. This is a correctness release for Source Explorer and its Reverse Dependency Graph. It changes neither the source registration workflow nor database entities.

### Why Rider showed 5, Tree 5, Graph 3

- **Reference locations** count distinct source occurrences (e.g. 5 locations in 4 files).
- **Graph vertices** are declarations owning those occurrences; multiple references within one declaration are legitimately grouped.
- **Previous defect:** `GetReferencesAsync()` used candidate `SourceRelation` rows from the same file and **line** to reconstruct the owner, and chose the first matching project. It could attach references to an unrelated method or namespace. This also allowed a plus click to expand an unrelated method, generating misleading totals such as 924.

### What changed

1. `SourceReferenceOwnership.Find` is one conservative syntax/semantic ownership resolver. It walks upward from the actual C# token to its containing method, constructor, local function, property, indexer, event, field, enum member, delegate or type. It does **not** return a namespace and does not infer owner by matching source lines.
2. Source usages record a verified owner when that declaration is actually indexed. Inaccessible/unresolved/malformed situations keep their **exact file/line** in Tree but never gain a speculative expandable graph vertex.
3. MediatR `Send/Publish` synthetic dispatch references use the same containing-declaration resolver and require the consumer to be an indexed symbol.
4. New **index version 6** distinguishes verified owners from v1–v5 data. Older indexes can load but graph ownership from them is unverified. **Reindex is required** after upgrading for correct graph traversal.
5. Source Explorer explicitly distinguishes reference locations, project/file count, and graph-vertex count; it warns about unverified owners.
6. Expansion warnings are now specific to the selected graph vertex's details panel and clear when that branch closes. A reference count from another vertex can no longer masquerade as the searched symbol's count.
7. Implementation/override/inheritance relationships remain available as source-grounded declaration locations. Their owners are only marked verified in v6.

### Regression fixture

A standalone .NET 8 console regression suite is included:

```powershell
dotnet run --project tests/SourceIntelligenceOwnershipRegression/SourceIntelligenceOwnershipRegression.csproj
```

It covers:
- Five references of `CheckSaveInspectionCertificateBillOfLading` across four files: handler interface, `Handle` parameter, two ordinary consumers, and one test method.
- References inside a handler's interface list belong to the handler **class**, while parameter references belong to `Handle()`.
- Multiple methods on the **same physical line** retain different owners.
- Field initializers belong to the field; local variables used in methods roll up to the method.
- None of those references are assigned to Namespace.

This fixture does not require access to your private code. It tests ownership semantics, not full repository load or all MediatR resolution paths.

### Upgrade and real-world validation

1. Back up the existing index files. Deploy the new source and build CENOPS.
2. Open **ابزارها → مدیریت منابع** and **بازایندکس** the affected solution. Do this before testing the graph; v5 owners are intentionally not trusted.
3. Search the class `CheckSaveInspectionCertificateBillOfLading` and open Tree. Compare the five file/line locations with Rider.
4. Confirm Tree says **5 references / 4 files** when all those sources are included in the registered index scope.
5. Confirm Graph shows only real owner declarations (no unrelated `GetAdminCheckByHsCodeQueryHandler.Handle()`, no Namespace).
6. Expand a verified method and compare the next-hop references against Rider's **Find Usages for that exact method**. The number may differ from the root class's five references: it belongs to the expanded method only.
7. Collapse a graph branch and check that its count/limit notice disappears from the selected vertex.
8. If some references are unresolved, review the source-index limits, solution scope, and unavailable external dependencies; do not assume the graph is complete.

### Constraints and security

- No SQL migration or new production NuGet dependency.
- Existing permissions and registered-folder path protections are unchanged.
- Import/indexing still does not restore, build, or execute source projects.
- Incomplete semantic models remain possible for copied solutions with missing dependencies/generated files.
- Root direct reference locations are not silently discarded when an owning declaration is uncertain.

### Verification completed in packaging environment

- ZIP archive integrity, changed-file comparison, project XML, JavaScript syntax, source-level contract checks.
- **Not performed:** full .NET/Razor compilation, execution of the regression test, indexing against the private enterprise source, or SQL/Blazor integration tests (no `dotnet` SDK is installed here).
