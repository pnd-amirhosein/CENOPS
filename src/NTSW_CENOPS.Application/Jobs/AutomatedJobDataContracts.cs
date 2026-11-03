namespace NTSW_CENOPS.Application.Jobs;

/// <summary>The complete daily JSON document, when explicitly requested.</summary>
public sealed record AutomatedJobDataModel(
    long Id,
    long AutomatedJobId,
    DateOnly Date,
    string Value,
    DateTimeOffset LastCalledAtUtc);

/// <summary>Lightweight day index, without loading the JSON payload.</summary>
public sealed record AutomatedJobDataDayModel(
    long Id,
    long AutomatedJobId,
    DateOnly Date,
    DateTimeOffset LastCalledAtUtc);

/// <summary>
/// Storage-only contract. It does not execute jobs or attach to the scheduler.
/// Callers must authorize access before invoking this service from a user-facing endpoint.
/// </summary>
public interface IAutomatedJobDataService
{
    Task<AutomatedJobDataModel?> GetDayAsync(
        long jobId, DateOnly date, CancellationToken ct = default);

    Task<IReadOnlyList<AutomatedJobDataDayModel>> ListDaysAsync(
        long jobId, DateOnly? from = null, DateOnly? through = null,
        int take = 90, CancellationToken ct = default);

    /// <summary>
    /// Upserts exactly one window within the specified daily document.
    /// rowsJson must be a JSON array. Replaying the same (start,end) window
    /// replaces it rather than duplicating it. Date is explicit so the caller
    /// controls calendar-day boundaries.
    /// </summary>
    Task<AutomatedJobDataModel> UpsertWindowAsync(
        long jobId, DateOnly date, DateTimeOffset windowStartUtc,
        DateTimeOffset windowEndUtc, string rowsJson,
        CancellationToken ct = default);

    Task<bool> DeleteDayAsync(long jobId, DateOnly date, CancellationToken ct = default);
}
