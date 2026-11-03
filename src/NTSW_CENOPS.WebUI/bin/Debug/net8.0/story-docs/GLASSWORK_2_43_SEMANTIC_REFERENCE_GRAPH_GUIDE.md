# Glasswork 2.43 — Semantic Reference Graph

**Base:** Glasswork 2.42. **Scope:** Source Intelligence graph meaning and vertex/tooltip UX. This is a focused source update; indexing, exact declaration search, source registration, tree view, permissions, and full-file modal remain intact.

## Behavior contract

The graph is a **progressive inverse-reference graph of meaningful C# declarations**, not a syntactic tree of files/classes/lines.

1. The root is the exact declaration chosen from Source Explorer.
2. For each reference to the root, the indexer resolves the **enclosing declaration of the occurrence** via Roslyn's symbol identity. A reference inside a method maps to that exact method; a reference inside a property maps to that property; a reference inside a type declaration can map to the type itself.
3. Group multiple occurrences in the same declaration into **one vertex with an occurrence count**. Files, namespaces and containing types are metadata, not invented intermediate edges.
4. The **plus button on a method (or any other resolved declaration)** requests `ISourceIntelligenceService.GetReferencesAsync(actor, repositoryId, thatVertex.ExpandSymbolId)`. That is the exact method/property/class declaration ID—not its declaring class ID and not the root's ID.
5. Return the next layer of **enclosing declarations**. Repeat only when another vertex is explicitly expanded. Never recursively load all branches.
6. If Roslyn cannot resolve a meaningful declaration, show an explicitly **Unresolved** terminal vertex (file/line), without guessing a class or method relationship.
7. Stop a branch when the same symbol identity repeats on its ancestry (cycle protection); keep distinct branches independent.

### Illustrative example

```csharp
public class Invoice { }
public DbSet<Invoice> Invoices { get; set; }

public Invoice UpdateInvoice()
{
    Invoice invoice = new();
    return invoice;
}

public IActionResult GetAll() => _service.UpdateInvoice();
```

If the indexed relationships resolve as shown:

- **Invoice (root)** → **Invoices (property)** because its declaration references the entity.
- **Invoice (root)** → **UpdateInvoice() (method)** because its body references/constructs the entity. The local variable is a reference occurrence, **not** the vertex.
- Clicking **plus on UpdateInvoice()** → **GetAll() (method)** because GetAll references that **method**. It does not expand `InvoiceService` as a class and does not collect unrelated usages of the class.

A real graph only shows edges supported by indexed source; it is not a guaranteed runtime call graph. Runtime DI, reflection, generated code, unresolved external packages and dynamic calls may not resolve statically.

## Changes

### Index-service output

`SourceUsage` has a new optional, **request-time** `ContainingDeclaration` field (`SourceSymbol?`). `GetReferencesAsync` resolves it from verified `MemberSymbolId` / `TypeSymbolId`, with a best-effort older-index fallback. The existing serialized index remains version 4 and backward compatible: no new indexing format, migration, NuGet dependency, or configuration is required.

### Graph projection

`SourceExplorerPage.BuildGraph()` creates one node per **enclosing declaration ID per path**, with the source occurrence count and code context. It no longer creates file/type/member/line wrappers that suggested false dependency hops. Each node carries its own expansion symbol ID and the clicked occurrence for the existing source viewer. The projection is bounded by the existing per-hop count, total node count and maximum depth.

### Visual design and interaction

- Consistent type-coded card backgrounds and accent strips for classes, methods, properties and other declarations.
- Compact type badge, readable full/wrapped title, declaring context and reference-count text.
- Reworked hover card: controlled maximum width/height; ellipsis on tooltip-only long lines; wrapped and clipped code/path content; position clamped to actual measured card dimensions and graph bounds.
- Selected state uses background/border color rather than a thick outline.
- The root is initially expanded, while discovered declarations are initially **collapsed**. This fixes a bug where clicking the plus on a first-level vertex accidentally collapsed it as its fetched references arrived.
- Keyboard expansion, node dragging, zoom/pan, path highlighting, branch focus and fullscreen source viewer remain available.

## Modified source files

- `src/HealthMonitoringApp.Application/SourceIntelligence/SourceModels.cs`
- `src/HealthMonitoringApp.Infrastructure/SourceIntelligence/SourceIntelligenceService.cs`
- `src/HealthMonitoringApp.WebUI/Components/Common/SourceGraphModels.cs`
- `src/HealthMonitoringApp.WebUI/Components/Common/SourceGraphView.razor`
- `src/HealthMonitoringApp.WebUI/Components/Common/SourceGraphView.razor.css`
- `src/HealthMonitoringApp.WebUI/Components/Pages/SourceExplorerPage.razor`
- `src/HealthMonitoringApp.WebUI/wwwroot/js/source-relation-graph.js`

## Deployment

1. Replace project source with Glasswork 2.43 (or the changed-files package).
2. `dotnet restore` and `dotnet build` from the solution root.
3. Restart the deployed web application to load the changed graph JavaScript (`v=2.43`).
4. Existing v4 indexes are usable without reindexing. For older indexes or unresolved owners, run **بازایندکس** once on the source-management page to improve semantic owner IDs.

No EF migration, new service process, or configuration change is required.

## Local validation checklist

- [ ] Search the **Invoice** or **BillOfLading** exact declaration. Confirm its graph shows methods/properties where referenced, not separate file/namespace/class wrappers around every usage.
- [ ] Confirm that multiple references inside the same method result in one method vertex with the correct occurrence count.
- [ ] Click **plus on GetInvoice()**. Compare the next-level results with Visual Studio **Find All References** on *GetInvoice()* itself, not its containing repository class.
- [ ] Click plus on the next method, then another; confirm only one hop is loaded on each click.
- [ ] Test two different methods on the same class: expanding one must not include callers of the other.
- [ ] Test a self-call / cyclic chain. Confirm it does not expand forever.
- [ ] Open a usage, then expand its full-file modal while the graph is fullscreen. Check that fullscreen remains active.
- [ ] Test a method with an exceptionally long signature, namespace, file path and C# code line. Hover text must stay entirely within its tooltip and graph viewport.
- [ ] Verify pan/zoom, selected state, collapse/re-expand, reset, keyboard activation and search filtering.
- [ ] Try an older index or a source with missing dependencies: unresolved contexts must not be mislabeled as verified dependencies.

## Validation performed here

- JavaScript `node --check` passed.
- Chromium tested long-label rendering, tooltip boundary containment, click/keyboard expansion and **two consecutive on-demand expansion callbacks** with distinct IDs.
- CSS parsing, ZIP integrity and targeted source assertions passed.

**Not verified:** a real .NET/Razor build, full Blazor integration and Roslyn symbol resolution against your private solution. Please compare the graph with Visual Studio before trusting it for impact analysis.
