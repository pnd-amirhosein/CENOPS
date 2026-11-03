using System.Security.Claims;

namespace NTSW_CENOPS.Application.Authentication;

public interface IUserPasswordService
{
    Task<PasswordChangeResult> ChangeCurrentUserPasswordAsync(
        ClaimsPrincipal principal,
        string currentPassword,
        string newPassword,
        string confirmPassword,
        CancellationToken cancellationToken = default);
}
