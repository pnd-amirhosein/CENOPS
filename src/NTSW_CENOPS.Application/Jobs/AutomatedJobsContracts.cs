using System.Security.Claims;

namespace NTSW_CENOPS.Application.Jobs;

public sealed class AutomatedJobModel
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public int IterationMinutes { get; set; } = 60;
    public string SourceKey { get; set; } = "SrLog";
    public int UserId { get; set; }
    public string AuthorDisplayName { get; set; } = "";
    public string SqlValue { get; set; } = "";
    public bool IsEnabled { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public DateTimeOffset? LastCalledAtUtc { get; set; }
    public string LastResult { get; set; } = "هنوز اجرا نشده";
    public DateTimeOffset? LastScheduledAtUtc { get; set; }
}

public sealed record AutomatedJobRunModel(long Id, long JobId, DateTimeOffset? ScheduledAtUtc,
    string Trigger, string Status, DateTimeOffset StartedAtUtc, DateTimeOffset? EndedAtUtc,
    int? RowsRead, string? ResultPreview, string? Error);

public sealed record JobOperationResult(bool Succeeded, string Message)
{
    public static JobOperationResult Ok(string message = "عملیات انجام شد.") => new(true, message);
    public static JobOperationResult Fail(string message) => new(false, message);
}

// Safe public description only: connection strings are never sent to the browser.
public sealed record JobSourceOption(string Key, string Server, string Database)
{
    public string DisplayName => $"{Server} / {Database}";
}

public interface IJobSourceCatalog
{
    IReadOnlyList<JobSourceOption> GetSources();
    bool IsConfigured(string key);
}

public interface IAutomatedJobService
{
    Task<IReadOnlyList<AutomatedJobModel>> ListAsync(ClaimsPrincipal actor, CancellationToken ct = default);
    Task<IReadOnlyList<AutomatedJobRunModel>> RecentRunsAsync(long id, ClaimsPrincipal actor, CancellationToken ct = default);
    Task<JobOperationResult> SaveAsync(AutomatedJobModel job, ClaimsPrincipal actor, CancellationToken ct = default);
    Task<JobOperationResult> UpdateSqlAsync(long id, string sql, ClaimsPrincipal actor, CancellationToken ct = default);
    Task<JobOperationResult> DeleteAsync(long id, ClaimsPrincipal actor, CancellationToken ct = default);
    Task<JobOperationResult> TestAsync(long id, ClaimsPrincipal actor, CancellationToken ct = default);
    Task<bool> CanManageAsync(ClaimsPrincipal actor, CancellationToken ct = default);
    Task<bool> CanTestAsync(ClaimsPrincipal actor, CancellationToken ct = default);
}
