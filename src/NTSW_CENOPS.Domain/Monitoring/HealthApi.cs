namespace NTSW_CENOPS.Domain.Monitoring;

public class HealthApi
{
    public int Id { get; set; }

    public string Name { get; set; } = default!;

    public string Url { get; set; } = default!;

    public APIType Type { get; set; } = APIType.Customs;

    public bool IsActive { get; set; } = true;

    public int IntervalMinutes { get; set; } = 5;

    public int TimeoutSeconds { get; set; } = 5;
}
