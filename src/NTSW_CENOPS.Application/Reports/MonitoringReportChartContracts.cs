using System.Security.Claims;

namespace NTSW_CENOPS.Application.Reports;

/// <summary>A bounded, chart-ready projection of persisted job results; never raw SQL rows.</summary>
public sealed record MonitoringChartPoint(string Label, double AxisValue, IReadOnlyList<double?> Measures);

public sealed record MonitoringChartData(
    long ReportId, string Name, string JobName, string ChartType,
    string ComparisonField, IReadOnlyList<string> MeasureNames,
    IReadOnlyList<MonitoringChartPoint> Points, string? Notice = null);

public interface IMonitoringReportChartService
{
    Task<IReadOnlyList<MonitoringChartData>> GetGalleryAsync(
        ClaimsPrincipal actor, CancellationToken ct = default);
}
