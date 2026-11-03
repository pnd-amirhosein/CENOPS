using NTSW_CENOPS.Application.Common.Identity;
using NTSW_CENOPS.Domain.Authorization;

namespace NTSW_CENOPS.Application.Authentication;

public sealed class AuthService : IAuthService
{
    private const int MaxDisplayNameLength = 150;
    private const int MaxUsernameLength = 100;

    private readonly IIdentityGateway _identityGateway;

    public AuthService(IIdentityGateway identityGateway)
    {
        _identityGateway = identityGateway;
    }

    public async Task<AuthServiceResult> LoginAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        username = username.Trim();

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            return AuthServiceResult.Failure(AuthErrorCode.RequiredFields);
        }

        var result = await _identityGateway.SignInAsync(username, password, cancellationToken);

        if (result.Succeeded)
        {
            return AuthServiceResult.Success();
        }

        return result.ErrorCode == IdentityErrorCode.LockedOut
            ? AuthServiceResult.Failure(AuthErrorCode.LockedOut)
            : AuthServiceResult.Failure(AuthErrorCode.InvalidCredentials);
    }

    public async Task<AuthServiceResult> SignupAsync(
        string displayName,
        string username,
        string password,
        string confirmPassword,
        CancellationToken cancellationToken = default)
    {
        displayName = displayName.Trim();
        username = username.Trim();

        if (string.IsNullOrWhiteSpace(displayName) ||
            string.IsNullOrWhiteSpace(username) ||
            string.IsNullOrWhiteSpace(password) ||
            string.IsNullOrWhiteSpace(confirmPassword))
        {
            return AuthServiceResult.Failure(AuthErrorCode.RequiredFields);
        }

        if (!string.Equals(password, confirmPassword, StringComparison.Ordinal))
        {
            return AuthServiceResult.Failure(AuthErrorCode.PasswordMismatch);
        }

        if (displayName.Length > MaxDisplayNameLength)
        {
            return AuthServiceResult.Failure(AuthErrorCode.DisplayNameTooLong);
        }

        if (username.Length > MaxUsernameLength)
        {
            return AuthServiceResult.Failure(AuthErrorCode.UsernameTooLong);
        }

        var result = await _identityGateway.CreateUserAsync(
            displayName,
            username,
            password,
            RoleNames.User,
            cancellationToken);

        return result.Succeeded
            ? AuthServiceResult.Success()
            : MapSignupFailure(result);
    }

    public Task LogoutAsync() => _identityGateway.SignOutAsync();

    private static AuthServiceResult MapSignupFailure(IdentityGatewayResult result) => result.ErrorCode switch
    {
        IdentityErrorCode.DuplicateUsername => AuthServiceResult.Failure(AuthErrorCode.DuplicateUsername),
        IdentityErrorCode.DuplicateDisplayName => AuthServiceResult.Failure(AuthErrorCode.DuplicateDisplayName),
        IdentityErrorCode.InvalidUsername => AuthServiceResult.Failure(AuthErrorCode.InvalidUsername),
        IdentityErrorCode.WeakPassword => AuthServiceResult.Failure(AuthErrorCode.WeakPassword, result.ErrorDetails),
        IdentityErrorCode.RoleCreationFailed => AuthServiceResult.Failure(AuthErrorCode.RoleCreationFailed),
        _ => AuthServiceResult.Failure(AuthErrorCode.SignupFailed)
    };
}
