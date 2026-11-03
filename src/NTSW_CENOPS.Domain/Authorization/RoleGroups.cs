namespace NTSW_CENOPS.Domain.Authorization;

/// <summary>
/// Reusable role combinations for UI visibility and route authorization.
/// ASP.NET Core treats comma-separated roles as OR conditions.
/// </summary>
public static class RoleGroups
{
    public const string OwnerOnly = RoleNames.Owner;
    public const string AdminOrOwner = RoleNames.Admin + "," + RoleNames.Owner;
}
