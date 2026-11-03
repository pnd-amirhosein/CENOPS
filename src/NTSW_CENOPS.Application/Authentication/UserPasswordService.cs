using System.Security.Claims;
using NTSW_CENOPS.Application.Common.Identity;

namespace NTSW_CENOPS.Application.Authentication;

public sealed class UserPasswordService : IUserPasswordService
{
    private readonly IIdentityGateway _identityGateway;

    public UserPasswordService(IIdentityGateway identityGateway)
    {
        _identityGateway = identityGateway;
    }

    public async Task<PasswordChangeResult> ChangeCurrentUserPasswordAsync(
        ClaimsPrincipal principal,
        string currentPassword,
        string newPassword,
        string confirmPassword,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(currentPassword) ||
            string.IsNullOrWhiteSpace(newPassword) ||
            string.IsNullOrWhiteSpace(confirmPassword))
        {
            return PasswordChangeResult.Failure(PasswordChangeErrorCode.RequiredFields);
        }

        if (!string.Equals(newPassword, confirmPassword, StringComparison.Ordinal))
        {
            return PasswordChangeResult.Failure(PasswordChangeErrorCode.PasswordMismatch);
        }

        var result = await _identityGateway.ChangePasswordAsync(
            principal,
            currentPassword,
            newPassword,
            cancellationToken);

        if (result.Succeeded)
        {
            return PasswordChangeResult.Success();
        }

        return result.ErrorCode switch
        {
            IdentityErrorCode.UserNotFound => PasswordChangeResult.Failure(PasswordChangeErrorCode.UserNotFound),
            IdentityErrorCode.CurrentPasswordInvalid => PasswordChangeResult.Failure(PasswordChangeErrorCode.CurrentPasswordInvalid),
            IdentityErrorCode.WeakPassword => PasswordChangeResult.Failure(PasswordChangeErrorCode.WeakPassword, result.ErrorDetails),
            _ => PasswordChangeResult.Failure(PasswordChangeErrorCode.ChangeFailed)
        };
    }
}
