using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using NTSW_CENOPS.Application.Access;
using NTSW_CENOPS.Application.Reports;
using NTSW_CENOPS.Domain.Authorization;
using NTSW_CENOPS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace NTSW_CENOPS.Infrastructure.Reports;

/// <summary>
/// Read-only reporting projection. All points come from saved AutomatedJobData;
/// never re-executes production SQL. Bounded days / points protect the Blazor circuit.
/// </summary>
public sealed class MonitoringReportChartService(
    IDbContextFactory<AppDbContext> factory, IAccessService access) : IMonitoringReportChartService
{
    private const int RecentDays = 14;
    private const int MaxMeasures = 12;
    private const int MaxPoints = 90;

    private sealed record RawPoint(double Axis, string Label, double?[] Measures, DateTimeOffset Collected);

    public async Task<IReadOnlyList<MonitoringChartData>> GetGalleryAsync(
        ClaimsPrincipal actor, CancellationToken ct = default)
    {
        if (!await access.HasAccessAsync(actor, AccessKeys.ViewReports, ct))
            throw new UnauthorizedAccessException("مجوز مشاهده گزارش‌ها را ندارید.");

        await using var db = await factory.CreateDbContextAsync(ct);
        var definitions = await (from report in db.MonitoringReports.AsNoTracking()
                                 join job in db.AutomatedJobs.IgnoreQueryFilters().AsNoTracking()
                                     on report.AutomatedJobId equals job.Id
                                 orderby report.UpdatedAtUtc descending, report.Id descending
                                 select new { report.Id, report.Name, report.AutomatedJobId,
                                     report.ComparisonField, report.ChartType, report.MeasureFieldsJson,
                                     JobName = job.Name })
            .ToListAsync(ct);

        var output = new List<MonitoringChartData>(definitions.Count);
        // Share per-job loaded days between reports configured against the same job.
        var storedDays = new Dictionary<long, List<string>>();
        foreach (var report in definitions)
        {
            ct.ThrowIfCancellationRequested();
            string[] measures;
            try
            {
                measures = JsonSerializer.Deserialize<string[]>(report.MeasureFieldsJson) ?? [];
                if (measures.Length is < 1 or > MaxMeasures ||
                    measures.Any(string.IsNullOrWhiteSpace) || measures.Contains(report.ComparisonField))
                    throw new JsonException("Invalid measure configuration");
            }
            catch (JsonException)
            {
                output.Add(new(report.Id, report.Name, report.JobName, report.ChartType,
                    report.ComparisonField, [], [], "ساختار اندازه‌گیری‌های گزارش معتبر نیست؛ گزارش را ویرایش کنید."));
                continue;
            }

            if (!storedDays.TryGetValue(report.AutomatedJobId, out var days))
            {
                days = await db.AutomatedJobData.AsNoTracking()
                    .Where(x => x.AutomatedJobId == report.AutomatedJobId)
                    .OrderByDescending(x => x.Date).Take(RecentDays)
                    .Select(x => x.Value).ToListAsync(ct);
                storedDays.Add(report.AutomatedJobId, days);
            }
            output.Add(Build(report.Id, report.Name, report.JobName, report.ChartType,
                report.ComparisonField, measures, days));
        }
        return output;
    }

    private static MonitoringChartData Build(long id, string name, string job, string chart,
        string comparison, string[] measures, List<string> days)
    {
        MonitoringChartData Result(IReadOnlyList<MonitoringChartPoint> points, string? message = null) =>
            new(id, name, job, chart, comparison, measures, points, message);

        if (days.Count == 0) return Result([], "هنوز داده‌ای برای این گزارش ثبت نشده است.");
        // Duplicate X values are common when job SQL returns rolling historical ranges.
        // The most recently collected matching point wins; do not sum duplicates.
        var points = new Dictionary<double, RawPoint>();
        var rejected = 0;
        foreach (var dailyJson in days)
        {
            try
            {
                using var document = JsonDocument.Parse(dailyJson);
                if (document.RootElement.ValueKind != JsonValueKind.Array) { rejected++; continue; }
                foreach (var window in document.RootElement.EnumerateArray())
                {
                    if (window.ValueKind != JsonValueKind.Object ||
                        !window.TryGetProperty("rows", out var rows) || rows.ValueKind != JsonValueKind.Array)
                    { rejected++; continue; }
                    var collected = ReadCollected(window);
                    foreach (var row in rows.EnumerateArray())
                    {
                        if (row.ValueKind != JsonValueKind.Object ||
                            !row.TryGetProperty(comparison, out var key) || !TryAxis(key, out var axis, out var label))
                        { rejected++; continue; }
                        var values = new double?[measures.Length];
                        var valid = true;
                        for (var i = 0; i < measures.Length; i++)
                        {
                            if (!row.TryGetProperty(measures[i], out var value)) { valid = false; break; }
                            if (value.ValueKind == JsonValueKind.Null) continue; // a gap, not zero
                            if (value.ValueKind != JsonValueKind.Number || !value.TryGetDouble(out var number) ||
                                double.IsNaN(number) || double.IsInfinity(number))
                            { valid = false; break; }
                            values[i] = number;
                        }
                        if (!valid) { rejected++; continue; }
                        // Prefer the latest successful collection; same X is not a separate sample.
                        if (!points.TryGetValue(axis, out var prior) || collected > prior.Collected)
                            points[axis] = new RawPoint(axis, label, values, collected);
                    }
                }
            }
            catch (JsonException) { rejected++; }
        }

        if (points.Count == 0)
            return Result([], "هیچ ردیف سازگاری با ساختار گزارش یافت نشد؛ داده یا تنظیمات گزارش را بررسی کنید.");

        var ordered = points.Values.OrderBy(x => x.Axis).ToArray();
        // Composition charts aggregate across the *whole* loaded range. Never
        // downsample every Nth hour before SUM(): that changes the percentages.
        // The cap protects Blazor circuits. If exceeded, show exactly what is omitted.
        var isComposition = chart is "pie" or "doughnut" or "treemap" or "spider" or "gauge";
        var limit = isComposition ? 2500 : chart is "bar" or "stacked" ? 32 :
            chart == "lollipop" ? 48 : MaxPoints;
        var omitted = Math.Max(0, ordered.Length - limit);
        if (omitted > 0)
            ordered = isComposition ? ordered.TakeLast(limit).ToArray() :
                Enumerable.Range(0, limit)
                    .Select(i => ordered[(int)Math.Round((double)i * (ordered.Length - 1) / (limit - 1))])
                    .ToArray();
        var projected = ordered.Select(x => new MonitoringChartPoint(x.Label, x.Axis, x.Measures)).ToArray();
        var messages = new List<string>();
        if (rejected > 0) messages.Add($"{rejected:N0} ردیف یا پنجره ناسازگار نادیده گرفته شد.");
        if (omitted > 0 && isComposition)
            messages.Add($"برای جلوگیری از بارگذاری بیش از حد، سهم‌ها فقط از {limit:N0} نقطهٔ آخر محاسبه شده‌اند؛ {omitted:N0} نقطهٔ قدیمی‌تر وارد محاسبه نشد.");
        return Result(projected, messages.Count == 0 ? null : string.Join(" ", messages));
    }

    private static DateTimeOffset ReadCollected(JsonElement element)
    {
        foreach (var key in new[] { "collectedAtUtc", "windowEndUtc" })
            if (element.TryGetProperty(key, out var prop) && prop.ValueKind == JsonValueKind.String &&
                DateTimeOffset.TryParse(prop.GetString(), CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal, out var time)) return time.ToUniversalTime();
        return DateTimeOffset.MinValue;
    }

    private static bool TryAxis(JsonElement value, out double axis, out string label)
    {
        axis = 0;
        label = "";
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out axis) &&
            !double.IsNaN(axis) && !double.IsInfinity(axis))
        { label = axis.ToString("G", CultureInfo.InvariantCulture); return true; }
        if (value.ValueKind == JsonValueKind.String &&
            DateTimeOffset.TryParse(value.GetString(), CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal, out var date))
        {
            axis = date.ToUniversalTime().UtcDateTime.Ticks;
            label = date.ToString("yyyy/MM/dd HH:mm", CultureInfo.InvariantCulture);
            return true;
        }
        return false;
    }
}
