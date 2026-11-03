namespace NTSW_CENOPS.Application.Administration;

public sealed record AdminOperationResult(bool Succeeded, string? ErrorMessage = null)
{
    public static AdminOperationResult Success() => new(true);

    public static AdminOperationResult Failure(string errorMessage) => new(false, errorMessage);
}
