using System.Security.Claims;
using System.Text.Json;
using NTSW_CENOPS.Application.Access;
using NTSW_CENOPS.Application.Reports;
using NTSW_CENOPS.Domain.Authorization;
using NTSW_CENOPS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace NTSW_CENOPS.Infrastructure.Reports;

public sealed class MonitoringReportService(IDbContextFactory<AppDbContext> factory, IAccessService access)
    : IMonitoringReportService
{
    private const int MaxNameLength = 180;
    private static readonly HashSet<string> ChartTypes = new(StringComparer.Ordinal)
        { "line", "bar", "scatter", "area", "step", "stacked", "smooth", "lollipop", "stackedarea",
          "pie", "doughnut", "treemap", "spider", "bubble", "gauge" };

    public Task<bool> CanManageAsync(ClaimsPrincipal actor, CancellationToken ct = default) =>
        access.HasAccessAsync(actor, AccessKeys.ManageReports, ct);

    private async Task AssertViewAsync(ClaimsPrincipal actor, CancellationToken ct)
    {
        if (!await access.HasAccessAsync(actor, AccessKeys.ViewReports, ct))
            throw new UnauthorizedAccessException("مجوز مشاهده گزارش‌ها را ندارید.");
    }

    public async Task<IReadOnlyList<MonitoringReportModel>> ListAsync(ClaimsPrincipal actor, CancellationToken ct = default)
    {
        await AssertViewAsync(actor, ct);
        await using var db = await factory.CreateDbContextAsync(ct);
        // Materialize database rows first; JSON deserialization is strictly in-memory.
        var rows = await (from report in db.MonitoringReports.AsNoTracking()
                          join job in db.AutomatedJobs.IgnoreQueryFilters().AsNoTracking()
                              on report.AutomatedJobId equals job.Id
                          orderby report.UpdatedAtUtc descending, report.Id descending
                          select new { Report = report, JobName = job.Name }).ToListAsync(ct);
        return rows.Select(x => ToModel(x.Report, x.JobName)).ToList();
    }

    public async Task<MonitoringReportModel?> GetAsync(long reportId, ClaimsPrincipal actor, CancellationToken ct = default)
    {
        await AssertViewAsync(actor, ct);
        await using var db = await factory.CreateDbContextAsync(ct);
        var row = await (from report in db.MonitoringReports.AsNoTracking()
                         join job in db.AutomatedJobs.IgnoreQueryFilters().AsNoTracking() on report.AutomatedJobId equals job.Id
                         where report.Id == reportId
                         select new { Report = report, JobName = job.Name }).SingleOrDefaultAsync(ct);
        if (row is null) return null;
        return ToModel(row.Report, row.JobName);
    }

    public async Task<IReadOnlyList<ReportJobOption>> ListJobsAsync(ClaimsPrincipal actor, CancellationToken ct = default)
    {
        await AssertViewAsync(actor, ct);
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.AutomatedJobs.AsNoTracking().OrderBy(x => x.Name)
            .Select(x => new ReportJobOption(x.Id, x.Name, x.SourceKey, x.IsEnabled)).ToListAsync(ct);
    }

    public async Task<ReportJobInspection> InspectJobAsync(long jobId, ClaimsPrincipal actor, CancellationToken ct = default)
    {
        await AssertViewAsync(actor, ct);
        await using var db = await factory.CreateDbContextAsync(ct);
        var job = await db.AutomatedJobs.AsNoTracking()
            .Where(x => x.Id == jobId)
            .Select(x => new { x.IsEnabled }).SingleOrDefaultAsync(ct);
        if (job is null) return ReportJobInspection.Empty("کار انتخاب‌شده یافت نشد.");

        // Read only the most recent DAILY RECORD, then its newest stored execution window.
        // Never execute SQL or fetch raw log data from production sources.
        var latest = await db.AutomatedJobData.AsNoTracking()
            .Where(x => x.AutomatedJobId == jobId)
            .OrderByDescending(x => x.Date)
            .Select(x => new { x.Date, x.Value })
            .FirstOrDefaultAsync(ct);
        if (latest is null)
            return ReportJobInspection.Empty(job.IsEnabled
                ? "این کار هنوز داده‌ای جمع‌آوری نکرده است. پس از اولین اجرای موفق دوباره تلاش کنید."
                : "این کار غیرفعال است و هنوز داده‌ای ذخیره نشده است.");
        return ReportDatasetInspector.Inspect(latest.Value, latest.Date);
    }

    public async Task<ReportSaveResult> SaveAsync(MonitoringReportModel model, ClaimsPrincipal actor, CancellationToken ct = default)
    {
        if (!await CanManageAsync(actor, ct)) return ReportSaveResult.Fail("مجوز مدیریت گزارش‌ها را ندارید.");
        var name = (model.Name ?? "").Trim();
        if (name.Length is < 2 or > MaxNameLength)
            return ReportSaveResult.Fail("نام گزارش باید بین ۲ تا ۱۸۰ نویسه باشد.");
        if (!ChartTypes.Contains(model.ChartType ?? ""))
            return ReportSaveResult.Fail("نوع نمودار معتبر نیست.");
        if (model.AutomatedJobId <= 0) return ReportSaveResult.Fail("یک کار را انتخاب کنید.");

        // Inspect under authorization again before saving: the preview from the wizard
        // can become stale after the job's latest execution or SQL changes.
        var inspection = await InspectJobAsync(model.AutomatedJobId, actor, ct);
        if (!inspection.IsEligible) return ReportSaveResult.Fail(inspection.Message);
        var chosen = inspection.Fields.SingleOrDefault(x => x.Name == model.ComparisonField && x.CanCompare);
        if (chosen is null) return ReportSaveResult.Fail("پارامتر مقایسه انتخاب‌شده در آخرین داده معتبر نیست.");
        // Persist the user's explicit numeric series selection. This matters for
        // composition charts: a total and its constituent counts overlap.
        var requested = model.MeasureFields ?? Array.Empty<string>();
        if (requested.Count == 0 || requested.Count > 12 ||
            requested.Distinct(StringComparer.Ordinal).Count() != requested.Count)
            return ReportSaveResult.Fail("بین یک تا دوازده اندازه‌گیری مجزا انتخاب کنید.");
        var compatible = inspection.Fields.Where(f => f.IsNumeric && f.Name != chosen.Name)
            .ToDictionary(f => f.Name, StringComparer.Ordinal);
        if (requested.Any(name => name is null || !compatible.ContainsKey(name)))
            return ReportSaveResult.Fail("یک یا چند اندازه‌گیری انتخاب‌شده دیگر عددی یا معتبر نیستند.");
        var minimum = model.ChartType == "spider" ? 3 :
            model.ChartType is "pie" or "doughnut" or "treemap" or "gauge" ? 2 : 1;
        if (requested.Count < minimum)
            return ReportSaveResult.Fail($"این نمودار دست‌کم {minimum} اندازه‌گیری عددی مجزا نیاز دارد.");

        var userId = int.TryParse(actor.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var uid) ? uid : 0;
        if (userId == 0) return ReportSaveResult.Fail("ورود به سامانه الزامی است.");

        await using var db = await factory.CreateDbContextAsync(ct);
        MonitoringReport record;
        if (model.Id == 0)
        {
            record = new MonitoringReport { CreatedByUserId = userId, CreatedAtUtc = DateTimeOffset.UtcNow };
            db.MonitoringReports.Add(record);
        }
        else
        {
            record = await db.MonitoringReports.SingleOrDefaultAsync(x => x.Id == model.Id, ct)
                ?? throw new KeyNotFoundException("گزارش یافت نشد.");
        }
        record.Name = name;
        record.AutomatedJobId = model.AutomatedJobId;
        record.ComparisonField = chosen.Name;
        record.ChartType = model.ChartType;
        record.MeasureFieldsJson = JsonSerializer.Serialize(requested);
        record.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return ReportSaveResult.Ok(record.Id);
    }

    public async Task<ReportSaveResult> DeleteAsync(long reportId, ClaimsPrincipal actor, CancellationToken ct = default)
    {
        if (!await CanManageAsync(actor, ct)) return ReportSaveResult.Fail("مجوز حذف گزارش را ندارید.");
        await using var db = await factory.CreateDbContextAsync(ct);
        var report = await db.MonitoringReports.SingleOrDefaultAsync(x => x.Id == reportId, ct);
        if (report is null) return ReportSaveResult.Fail("گزارش پیدا نشد.");
        db.MonitoringReports.Remove(report);
        await db.SaveChangesAsync(ct);
        return new ReportSaveResult(true, "گزارش حذف شد.");
    }

    private static MonitoringReportModel ToModel(MonitoringReport entity, string jobName) => new()
    {
        Id = entity.Id, Name = entity.Name,
        AutomatedJobId = entity.AutomatedJobId, JobName = jobName,
        ComparisonField = entity.ComparisonField, ChartType = entity.ChartType,
        MeasureFields = JsonSerializer.Deserialize<List<string>>(entity.MeasureFieldsJson) ?? [],
        CreatedAtUtc = entity.CreatedAtUtc, UpdatedAtUtc = entity.UpdatedAtUtc
    };
}
