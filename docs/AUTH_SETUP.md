# Authentication setup

Authentication is implemented with ASP.NET Core Identity + EF Core + SQL Server and is split by responsibility:

- `src/HealthMonitoringApp.Application/Authentication` — login/signup/password application use cases
- `src/HealthMonitoringApp.Application/Users` — profile-update use case
- `src/HealthMonitoringApp.Application/Common/Identity` — Identity abstraction used by Application
- `src/HealthMonitoringApp.Infrastructure/Identity` — ASP.NET Core Identity implementation and persistence types
- `src/HealthMonitoringApp.Infrastructure/Persistence/AppDbContext.cs` — Identity + monitoring DbContext
- `src/HealthMonitoringApp.WebUI/Endpoints/AuthEndpointExtensions.cs` — thin HTTP/form endpoints and redirects

`Program.cs` remains the composition root and does not contain authentication business logic.

## Reusable UI pieces

The existing auth/profile UI remains in WebUI without visual changes:

- `Components/Common/AppModal.razor` — reusable modal
- `Components/Common/PasswordInput.razor` — reusable password input with show/hide control
- `Components/Auth/AuthBrandPanel.razor` — shared authentication brand panel

## Configuration

`ConnectionStrings:Default` is read from the WebUI configuration. For deployed environments, override sensitive values with environment-specific configuration, User Secrets, or environment variables rather than committing credentials.

User Secrets example from the WebUI project directory:

```bash
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:Default" "Server=YOUR_SERVER;Database=HealthMonitorDb;User Id=YOUR_LOGIN;Password=YOUR_PASSWORD;TrustServerCertificate=True"
```

## Signup flow

Public page: `/signup`

Inputs:

- Display name
- Username
- Password
- Repeat password

Every public signup receives the `User` role. Server-side validation still enforces required fields, repeat-password equality, display/username length, uniqueness, username validity, and the configured Identity password policy.

Password policy:

- Minimum 8 characters
- At least one digit
- At least one lowercase letter
- At least one uppercase letter
- Non-alphanumeric characters are optional

## Change password flow

Authenticated page: `/change-password`

A successful password change signs the current user out and redirects back to Login, preserving the existing behavior.

## EF Core migrations

Migrations now live with persistence in Infrastructure.

From the solution root:

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

## Privileged users

Do not manually write Identity password hashes. Use `IdentityDataSeeder` / `UserManager<ApplicationUser>` so Identity generates password and security fields correctly.

## Role-protect a page

Any signed-in user:

```razor
@attribute [Authorize]
```

For role names in C# code, use `HealthMonitoringApp.Domain.Authorization.RoleNames`. The available roles are `Owner`, `Admin`, and `User`.

Reusable role groups live in `HealthMonitoringApp.Domain.Authorization.RoleGroups`:

- `RoleGroups.OwnerOnly`
- `RoleGroups.AdminOrOwner`

Protect a route with the standard authorization attribute:

```razor
@attribute [Authorize(Roles = RoleGroups.AdminOrOwner)]
```

Hide an element or menu entry with the reusable `RoleGate` component:

```razor
<RoleGate Roles="@RoleGroups.AdminOrOwner">
    ...
</RoleGate>
```

UI visibility is only a convenience; sensitive pages must also use `[Authorize(Roles = ...)]`.

## User profile menu

The Skyline account menu still reads the `display_name` claim and username. The claim constant is application-facing (`Application.Common.Security.AppClaimTypes`), while the claims principal factory that creates it remains an Infrastructure implementation.
