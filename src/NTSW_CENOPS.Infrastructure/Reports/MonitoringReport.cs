namespace NTSW_CENOPS.Infrastructure.Reports;

public sealed class MonitoringReport
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public long AutomatedJobId { get; set; }
    public string ComparisonField { get; set; } = "";
    public string ChartType { get; set; } = "line";
    public string MeasureFieldsJson { get; set; } = "[]";
    public int CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
