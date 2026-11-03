using System.Security.Claims;

namespace NTSW_CENOPS.Application.Common.Identity;

public interface IIdentityGateway
{
    Task<IdentityGatewayResult> SignInAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default);

    Task<IdentityGatewayResult> CreateUserAsync(
        string displayName,
        string username,
        string password,
        string defaultRole,
        CancellationToken cancellationToken = default);

    Task<IdentityGatewayResult> UpdateProfileAsync(
        ClaimsPrincipal principal,
        string displayName,
        string username,
        DateTime? birthDate,
        CancellationToken cancellationToken = default);

    Task<DateTime?> GetBirthDateAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default);

    Task<IdentityGatewayResult> ChangePasswordAsync(
        ClaimsPrincipal principal,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default);

    Task SignOutAsync();
}
