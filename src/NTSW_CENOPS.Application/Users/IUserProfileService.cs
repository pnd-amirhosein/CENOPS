using System.Security.Claims;

namespace NTSW_CENOPS.Application.Users;

public interface IUserProfileService
{
    Task<UserProfileUpdateResult> UpdateCurrentAsync(
        ClaimsPrincipal principal,
        string displayName,
        string username,
        DateTime? birthDate,
        CancellationToken cancellationToken = default);

    Task<DateTime?> GetCurrentBirthDateAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default);
}
