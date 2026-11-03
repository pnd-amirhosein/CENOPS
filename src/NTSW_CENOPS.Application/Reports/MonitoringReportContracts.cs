using System.Security.Claims;

namespace NTSW_CENOPS.Application.Reports;

/// <summary>A report uses one comparison field and all remaining numeric fields as measures.</summary>
public sealed class MonitoringReportModel
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public long AutomatedJobId { get; set; }
    public string JobName { get; set; } = "";
    public string ComparisonField { get; set; } = "";
    public string ChartType { get; set; } = "line";
    public IReadOnlyList<string> MeasureFields { get; set; } = Array.Empty<string>();
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

public sealed record ReportJobOption(long Id, string Name, string SourceKey, bool IsEnabled);

public sealed record ReportFieldInfo(string Name, string Type, bool CanCompare, bool IsNumeric);

public sealed record ReportJobInspection(
    bool HasData, bool IsEligible, string Message,
    IReadOnlyList<ReportFieldInfo> Fields,
    DateOnly? DataDate, int RowCount)
{
    public static ReportJobInspection Empty(string message) =>
        new(false, false, message, Array.Empty<ReportFieldInfo>(), null, 0);
}

public sealed record ReportSaveResult(bool Succeeded, string Message, long? ReportId = null)
{
    public static ReportSaveResult Fail(string message) => new(false, message);
    public static ReportSaveResult Ok(long id) => new(true, "گزارش با موفقیت ذخیره شد.", id);
}

public interface IMonitoringReportService
{
    Task<IReadOnlyList<MonitoringReportModel>> ListAsync(ClaimsPrincipal actor, CancellationToken ct = default);
    Task<MonitoringReportModel?> GetAsync(long reportId, ClaimsPrincipal actor, CancellationToken ct = default);
    Task<IReadOnlyList<ReportJobOption>> ListJobsAsync(ClaimsPrincipal actor, CancellationToken ct = default);
    Task<ReportJobInspection> InspectJobAsync(long jobId, ClaimsPrincipal actor, CancellationToken ct = default);
    Task<ReportSaveResult> SaveAsync(MonitoringReportModel model, ClaimsPrincipal actor, CancellationToken ct = default);
    Task<ReportSaveResult> DeleteAsync(long reportId, ClaimsPrincipal actor, CancellationToken ct = default);
    Task<bool> CanManageAsync(ClaimsPrincipal actor, CancellationToken ct = default);
}
