namespace NTSW_CENOPS.Application.Authentication;

public sealed record AuthServiceResult(
    bool Succeeded,
    AuthErrorCode ErrorCode = AuthErrorCode.None,
    IReadOnlyCollection<string>? ErrorDetails = null)
{
    public static AuthServiceResult Success() => new(true);

    public static AuthServiceResult Failure(
        AuthErrorCode errorCode,
        IEnumerable<string>? errorDetails = null) =>
        new(false, errorCode, errorDetails?.ToArray());
}
