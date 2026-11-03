# Architecture

The solution is split into four projects under `src/` and follows Clean Architecture dependency direction.

```text
HealthMonitoringApp.Domain
        ↑
HealthMonitoringApp.Application
        ↑
HealthMonitoringApp.Infrastructure
        ↑
HealthMonitoringApp.WebUI   (composition root)
```

`WebUI` also references `Application` and `Domain` directly for UI-facing contracts/domain types. `Application` never references `Infrastructure` or `WebUI`, and `Domain` has no project dependencies.

## HealthMonitoringApp.Domain

Contains business/domain types only:

- monitoring entities (`HealthApi`, `HealthCheckResult`)
- monitoring enum (`APIType`)
- `Contact`
- authorization role names

There are no EF Core, ASP.NET Identity, HTTP, or UI dependencies in this project.

## HealthMonitoringApp.Application

Contains application use cases and abstractions:

- authentication orchestration and validation
- password-change use case
- profile-update use case
- health-monitor polling orchestration
- identity, persistence, and health-probe abstractions
- application-level result/error models

Application code depends only on Domain plus framework abstractions such as logging/DI.

## HealthMonitoringApp.Infrastructure

Contains technical implementations:

- `AppDbContext`
- EF Core migrations
- ASP.NET Core Identity persistence types
- Identity gateway implementation
- claims principal factory
- EF health-monitor repository
- HTTP health probe
- background polling worker
- Infrastructure DI registration

The Identity `ApplicationUser` and `ApplicationRole` stay here intentionally. They inherit ASP.NET Identity persistence types, so moving them into Domain would make the Domain project depend on infrastructure/framework concerns.

## HealthMonitoringApp.WebUI

Contains presentation/host concerns only:

- Blazor components and scoped CSS
- auth HTTP endpoints
- `Program.cs`
- appsettings / launch settings
- UI-only state (`LoadingService`)
- data-table and sidebar UI models
- frontend package/Tailwind files

The WebUI assembly name remains `HealthMonitoringApp`, preserving the existing scoped CSS bundle URL `HealthMonitoringApp.styles.css`.

## Dependency registration

`Program.cs` composes the application with:

```csharp
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
```

This keeps the host small while preserving the existing authentication, polling, and UI behavior.

## EF Core commands

Run from the solution root:

```bash
dotnet ef migrations add <MigrationName> \
  --project src/HealthMonitoringApp.Infrastructure/HealthMonitoringApp.Infrastructure.csproj \
  --startup-project src/HealthMonitoringApp.WebUI/HealthMonitoringApp.WebUI.csproj \
  --context AppDbContext

dotnet ef database update \
  --project src/HealthMonitoringApp.Infrastructure/HealthMonitoringApp.Infrastructure.csproj \
  --startup-project src/HealthMonitoringApp.WebUI/HealthMonitoringApp.WebUI.csproj \
  --context AppDbContext
```
