using System.Security.Claims;

namespace NTSW_CENOPS.Application.Profiles;

public sealed record UserHoverProfile(string DisplayName, string Username, DateTime? BirthDate);

public interface IUserHoverProfileService
{
    Task<UserHoverProfile?> FindAsync(string? displayName, string? username, ClaimsPrincipal viewer, CancellationToken cancellationToken = default);
}
