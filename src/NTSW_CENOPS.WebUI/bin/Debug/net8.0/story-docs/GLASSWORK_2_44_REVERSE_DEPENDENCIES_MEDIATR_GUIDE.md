# Glasswork 2.44 — Reverse Dependency Graph & MediatR Bridges

**Baseline:** Glasswork 2.43  
**Scope:** Source Intelligence graph and the existing read-only Roslyn indexer  
**Migration:** None  
**Source-index update:** Version 5; reindex registered repositories after deploying  
**No imported code is compiled, restored, or executed by Source Intelligence.**

## 1. Expected direction of exploration

The graph answers **“Who consumes this exact declaration?”** at each expansion:

```text
Invoice (entity)
  └─ Invoices (AppDbContext property; DbSet<Invoice>)
      └─ GetInvoices() (repository method reading Invoices)
          └─ Handle(GetInvoicesQuery, ...) (handler using repository method)
              ╎ MediatR.Send — inferred runtime dispatch
              └─ GetAll() (controller method containing sender.Send(new GetInvoicesQuery()))
                  └─ Other actual callers, if any
```

Each plus action fetches **one hop of direct consumers for the selected declaration's exact symbol ID**. It does not replace `GetInvoices()` with references to its containing `Repository` class. Local references inside the same method are grouped under the enclosing method, and source locations remain available for inspection.

A graph branch does not automatically stop just because it reaches a controller. It stops when no additional indexed consumers exist, a cycle is reached, or an expansion limit applies. A controller is a possible *entry point*, not proof of an externally invoked route.

## 2. MediatR recognition

The additional indexing pass recognizes:

- `IRequestHandler<TRequest, TResponse>` and `IRequestHandler<TRequest>` handlers exposing `Handle`.
- `INotificationHandler<TNotification>` handlers exposing `Handle`.
- `IMediator.Send(...)` and `ISender.Send(...)` for requests.
- `IMediator.Publish(...)` and `IPublisher.Publish(...)` for notifications.
- `global using MediatR;` and ordinary `using MediatR;`, including projects whose external MediatR packages have not been restored by the safe indexer.

For a handler request type matching the concrete argument passed to `Send`, the indexer records a **synthetic, source-backed reference to the handler's Handle method at the sender's callsite**. When someone expands that handler vertex, the containing controller/service method appears as its direct consumer. Subsequent expansion uses the controller/service method's **own** symbol ID.

These relationships are **inferred framework dispatches**, not static calls to `Handle()`. The graph draws them with dashed lines and a MediatR badge, and the source-preview window opens the exact `Send`/`Publish` statement. The Tree tab shows the same badge.

### Important precision rules

1. A `Send` method on an unrelated bus is not considered MediatR. The receiver must resolve to `IMediator` / `ISender` (or an implementing type), with conservative fallback only when the source imports or qualifies MediatR.
2. The request is matched by its **Roslyn type identity**, not merely by comparing method names. If the concrete argument type cannot be resolved, **no edge is fabricated**.
3. For `Send`, if multiple handler implementations exist for the same request type, the indexer does not arbitrarily choose a target; that bridge is omitted. `Publish` can reach multiple notification handlers.
4. Runtime registration and dependency-injection containers are not executed; therefore the edges remain labeled **inferred** even when source types match.
5. Reflection, dynamic request construction, custom dispatcher wrappers, pre/post processors, pipeline behaviors, and some unavailable/generated or external symbols can remain unresolved. A missing edge does not prove no runtime connection.
6. Ordinary static references, interface declarations, and graph protections from 2.43 remain intact.

## 3. Implementation overview

- **`MediatRDispatchIndexer.cs` (new):** Separated static Roslyn analyzer for source-backed MediatR edges. No web UI dependency and no dependency on MediatR NuGet packages in CENOPS.
- **`SourceIntelligenceService.cs`:** Adds the inferred dispatch occurrences to the same references and relation index consumed by the rest of Source Intelligence. Publishes source-index version **5** and continues loading versions 1–4.
- **`SourceGraphModels.cs`:** Adds an optional relationship description to the reusable graph node contract.
- **`SourceExplorerPage.razor`:** Preserves consumer-first expansion, marks inferred MediatR usages, and propagates relation metadata to the graph.
- **`SourceGraphView.razor`, stylesheet, `source-relation-graph.js`:** Renders distinct inferred relationships in the node inspector, hover tooltip, and as dashed edges.
- **`SourceUsagePreviewDialog.razor`:** Shows the inferred-dispatch marker beside the original file/line information.

No new SQL tables, EF migration, or source-folder configuration changes are necessary.

## 4. Upgrade procedure

1. Back up the working Glasswork 2.43 source and source-index snapshots.
2. Replace the source files from the Changed Files ZIP, or deploy the full 2.44 project.
3. Run `dotnet restore` and `dotnet build` locally.
4. Under **ابزارها → مدیریت منابع**, run **بازایندکس** for each repository to create version-5 snapshots.
5. Open **ابزارها → تحلیل سورس**, resolve a known exact declaration, switch to **Graph**, and expand one level at a time.

**Note:** Existing version-4 indexes still load. They will not contain MediatR dispatch bridges until reindexed, and the reference view flags older indexes as potentially incomplete.

## 5. Suggested C# verification fixture

Place code shaped like the following in a registered, manually copied source tree. In a real project, the corresponding MediatR interfaces come from its normal dependency. The synthetic indexer does not restore or execute that dependency.

```csharp
using MediatR;

public sealed record GetInvoicesQuery() : IRequest<IReadOnlyList<Invoice>>;

public sealed class GetInvoicesHandler : IRequestHandler<GetInvoicesQuery, IReadOnlyList<Invoice>>
{
    private readonly IInvoiceRepository _repo;
    public GetInvoicesHandler(IInvoiceRepository repo) => _repo = repo;
    public Task<IReadOnlyList<Invoice>> Handle(GetInvoicesQuery request, CancellationToken ct)
        => _repo.GetInvoicesAsync(ct);
}

public sealed class InvoiceController
{
    private readonly ISender _sender;
    public InvoiceController(ISender sender) => _sender = sender;
    public Task<IReadOnlyList<Invoice>> GetAll(CancellationToken ct)
        => _sender.Send(new GetInvoicesQuery(), ct);
}
```

Expected for **Handle** after reindex:

- Expand `Handle(GetInvoicesQuery, ...)` and find `InvoiceController.GetAll` as a consumer.
- The reference location is the `Send(new GetInvoicesQuery(), ct)` line.
- The connection is marked `MediatR.Send (inferred)` and drawn dashed.
- Expand `GetAll` and fetch only references to **GetAll**, not every reference to `InvoiceController`.

Negative tests: unrelated `emailSender.Send(message)`, an unresolvable dynamic request, and two request handlers for the same type must **not** produce an arbitrary MediatR Send edge.

Also test `Publish(notification)` with two notification handlers; both may be inferred consumers from the publish site when they are indexed.

## 6. Verification limitations

The archives, JS syntax, CSS parsing, project XML, indexing integration, and Chromium graph behaviors were checked in the packaging environment. **The .NET SDK and real imported solutions were not available there**, so the C# build and live Roslyn/MediatR behavior are *not* claimed as passed. Run the C# fixture and compare against your real solution before relying on inferred paths.

For enterprise solutions that use interfaces heavily, a separate future pass may be required to correlate interface-call sites with concrete dependency-injected implementations. This release addresses MediatR's request-to-handler dispatch specifically; it does not make unverified general DI edges.
