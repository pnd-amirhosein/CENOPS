namespace NTSW_CENOPS.Application.Authentication;

public sealed record PasswordChangeResult(
    bool Succeeded,
    PasswordChangeErrorCode ErrorCode = PasswordChangeErrorCode.None,
    IReadOnlyCollection<string>? ErrorDetails = null)
{
    public static PasswordChangeResult Success() => new(true);

    public static PasswordChangeResult Failure(
        PasswordChangeErrorCode errorCode,
        IEnumerable<string>? errorDetails = null) =>
        new(false, errorCode, errorDetails?.ToArray());
}
