namespace NTSW_CENOPS.Infrastructure.Jobs;

public sealed class AutomatedJob
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public int IterationMinutes { get; set; } = 60;
    public string SourceKey { get; set; } = "SrLog";
    public int UserId { get; set; }
    public string SqlValue { get; set; } = "";
    public bool IsEnabled { get; set; } = true;
    public bool IsDeleted { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public DateTimeOffset? LastCalledAtUtc { get; set; }
    public DateTimeOffset? LastScheduledAtUtc { get; set; }
    public string LastResult { get; set; } = "هنوز اجرا نشده";
}

public sealed class AutomatedJobRun
{
    public long Id { get; set; }
    public long JobId { get; set; }
    public DateTimeOffset? ScheduledAtUtc { get; set; }
    public string Trigger { get; set; } = "Scheduled";
    public string Status { get; set; } = "Running";
    public DateTimeOffset StartedAtUtc { get; set; }
    public DateTimeOffset? EndedAtUtc { get; set; }
    public int? RowsRead { get; set; }
    public string? ResultPreview { get; set; }
    public string? Error { get; set; }
}
