using System.Security.Claims;

namespace NTSW_CENOPS.Application.Dashboards;

/// <summary>Global and shared user layouts remain separate; ownership and viewer defaults are independent.</summary>
public sealed record DashboardGlobalModel(string Title, string LayoutJson, DateTimeOffset? UpdatedAtUtc);
public sealed record UserDashboardModel(int Id, string Name, string LayoutJson, bool IsDefault, DateTimeOffset UpdatedAtUtc, int OwnerId, string OwnerName, bool CanEdit);
public sealed record DashboardWorkspaceModel(DashboardGlobalModel Global,
    IReadOnlyList<UserDashboardModel> PersonalDashboards, int SelectedDashboardId, bool CanEditGlobal);

public interface IDashboardConfigurationService
{
    Task<DashboardWorkspaceModel> GetWorkspaceAsync(ClaimsPrincipal actor, CancellationToken ct = default);
    Task<UserDashboardModel> CreatePersonalAsync(string name, ClaimsPrincipal actor, CancellationToken ct = default);
    Task SavePersonalAsync(int id, string name, string layoutJson, ClaimsPrincipal actor, CancellationToken ct = default);
    Task SaveGlobalAsync(string title, string layoutJson, ClaimsPrincipal actor, CancellationToken ct = default);
    Task SetDefaultAsync(int id, ClaimsPrincipal actor, CancellationToken ct = default);
    Task DeletePersonalAsync(int id, ClaimsPrincipal actor, CancellationToken ct = default);
}
