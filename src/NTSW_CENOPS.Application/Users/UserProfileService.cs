using System.Security.Claims;
using NTSW_CENOPS.Application.Common.Identity;

namespace NTSW_CENOPS.Application.Users;

public sealed class UserProfileService : IUserProfileService
{
    public Task<DateTime?> GetCurrentBirthDateAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default) =>
        _identityGateway.GetBirthDateAsync(principal, cancellationToken);

    private const int MaxDisplayNameLength = 150;
    private const int MaxUsernameLength = 100;

    private readonly IIdentityGateway _identityGateway;

    public UserProfileService(IIdentityGateway identityGateway)
    {
        _identityGateway = identityGateway;
    }

    public async Task<UserProfileUpdateResult> UpdateCurrentAsync(
        ClaimsPrincipal principal,
        string displayName,
        string username,
        DateTime? birthDate,
        CancellationToken cancellationToken = default)
    {
        displayName = displayName.Trim();
        username = username.Trim();

        if (string.IsNullOrWhiteSpace(displayName) || string.IsNullOrWhiteSpace(username))
        {
            return UserProfileUpdateResult.Failure(UserProfileErrorCode.RequiredFields);
        }

        if (displayName.Length > MaxDisplayNameLength)
        {
            return UserProfileUpdateResult.Failure(UserProfileErrorCode.DisplayNameTooLong);
        }

        if (username.Length > MaxUsernameLength)
        {
            return UserProfileUpdateResult.Failure(UserProfileErrorCode.UsernameTooLong);
        }

        if (birthDate is { } date && (date.Date > DateTime.Today || date.Year < 1000))
            return UserProfileUpdateResult.Failure(UserProfileErrorCode.InvalidBirthDate);

        var result = await _identityGateway.UpdateProfileAsync(
            principal,
            displayName,
            username,
            birthDate?.Date,
            cancellationToken);

        if (result.Succeeded)
        {
            return UserProfileUpdateResult.Success();
        }

        return result.ErrorCode switch
        {
            IdentityErrorCode.UserNotFound => UserProfileUpdateResult.Failure(UserProfileErrorCode.UserNotFound),
            IdentityErrorCode.DuplicateUsername => UserProfileUpdateResult.Failure(UserProfileErrorCode.DuplicateUsername),
            IdentityErrorCode.DuplicateDisplayName => UserProfileUpdateResult.Failure(UserProfileErrorCode.DuplicateDisplayName),
            IdentityErrorCode.InvalidUsername => UserProfileUpdateResult.Failure(UserProfileErrorCode.InvalidUsername),
            _ => UserProfileUpdateResult.Failure(UserProfileErrorCode.UpdateFailed)
        };
    }
}
