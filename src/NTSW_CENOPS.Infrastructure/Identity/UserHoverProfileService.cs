using System.Security.Claims;
using NTSW_CENOPS.Application.Profiles;
using NTSW_CENOPS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace NTSW_CENOPS.Infrastructure.Identity;

/// <summary>Read-only, authenticated profile projection for author hovers.
/// Never expose passwords, role membership or internal user identifiers.</summary>
public sealed class UserHoverProfileService(IDbContextFactory<AppDbContext> factory) : IUserHoverProfileService
{
    public async Task<UserHoverProfile?> FindAsync(string? displayName, string? username,
        ClaimsPrincipal viewer, CancellationToken cancellationToken = default)
    {
        if (viewer.Identity?.IsAuthenticated != true) return null;
        var exactUsername = username?.Trim();
        var exactName = displayName?.Trim();
        if (string.IsNullOrEmpty(exactUsername) && string.IsNullOrEmpty(exactName)) return null;
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        // Only return a display-name match when unique. Caller-supplied display names
        // should not accidentally resolve to a different user with the same name.
        var users = await db.Users.AsNoTracking()
            .Where(x => !x.IsDeleted && (exactUsername != null && exactUsername != ""
                ? x.NormalizedUserName == exactUsername.ToUpper()
                : x.DisplayName == exactName))
            .Select(x => new UserHoverProfile(x.DisplayName, x.UserName ?? "", x.BirthDate))
            .Take(2).ToListAsync(cancellationToken);
        return users.Count == 1 ? users[0] : null;
    }
}
