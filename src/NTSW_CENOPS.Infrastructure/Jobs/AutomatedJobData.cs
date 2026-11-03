namespace NTSW_CENOPS.Infrastructure.Jobs;

/// <summary>
/// One JSON document per job per calendar day. The caller chooses the date
/// (for example, the date of the monitored time window in the chosen timezone).
/// </summary>
public sealed class AutomatedJobData
{
    public long Id { get; set; }
    public long AutomatedJobId { get; set; }
    public DateOnly Date { get; set; }
    public string Value { get; set; } = "[]";
    public DateTimeOffset LastCalledAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
