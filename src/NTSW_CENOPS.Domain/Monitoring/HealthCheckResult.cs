namespace NTSW_CENOPS.Domain.Monitoring;

public class HealthCheckResult
{
    public long Id { get; set; }

    public int HealthApiId { get; set; }

    public HealthApi HealthApi { get; set; } = default!;

    public DateTimeOffset CheckedAt { get; set; }

    public bool IsSuccess { get; set; }

    public int? StatusCode { get; set; }

    public long DurationMs { get; set; }

    public string? ErrorMessage { get; set; }
}
