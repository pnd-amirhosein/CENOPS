namespace NTSW_CENOPS.Application.Users;

public sealed record UserProfileUpdateResult(
    bool Succeeded,
    UserProfileErrorCode ErrorCode = UserProfileErrorCode.None)
{
    public static UserProfileUpdateResult Success() => new(true);

    public static UserProfileUpdateResult Failure(UserProfileErrorCode errorCode) =>
        new(false, errorCode);
}
