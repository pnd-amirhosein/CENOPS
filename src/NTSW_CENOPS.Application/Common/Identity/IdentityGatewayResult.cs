namespace NTSW_CENOPS.Application.Common.Identity;

public sealed record IdentityGatewayResult(
    bool Succeeded,
    IdentityErrorCode ErrorCode = IdentityErrorCode.None,
    IReadOnlyCollection<string>? ErrorDetails = null)
{
    public static IdentityGatewayResult Success() => new(true);

    public static IdentityGatewayResult Failure(
        IdentityErrorCode errorCode,
        IEnumerable<string>? errorDetails = null) =>
        new(false, errorCode, errorDetails?.ToArray());
}
