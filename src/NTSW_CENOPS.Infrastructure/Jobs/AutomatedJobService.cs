using System.Security.Claims;
using NTSW_CENOPS.Application.Access;
using NTSW_CENOPS.Application.Jobs;
using NTSW_CENOPS.Domain.Authorization;
using NTSW_CENOPS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace NTSW_CENOPS.Infrastructure.Jobs;

public sealed class AutomatedJobService(IDbContextFactory<AppDbContext> factory,
    IAccessService access, AutomatedJobRunner runner, IJobSourceCatalog sources) : IAutomatedJobService
{
    public Task<bool> CanManageAsync(ClaimsPrincipal actor, CancellationToken ct = default) =>
        access.HasAccessAsync(actor, AccessKeys.ManageJobs, ct);

    public Task<bool> CanTestAsync(ClaimsPrincipal actor, CancellationToken ct = default) =>
        access.HasAccessAsync(actor, AccessKeys.ExecuteJobs, ct);

    private Task<bool> CanView(ClaimsPrincipal actor, CancellationToken ct) =>
        access.HasAccessAsync(actor, AccessKeys.ViewJobs, ct);

    private static int ActorId(ClaimsPrincipal actor) =>
        int.TryParse(actor.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;

    public async Task<IReadOnlyList<AutomatedJobModel>> ListAsync(ClaimsPrincipal actor, CancellationToken ct = default)
    {
        if (!await CanView(actor, ct)) throw new UnauthorizedAccessException("شما به کارهای زمان‌بندی‌شده دسترسی ندارید.");
        await using var db = await factory.CreateDbContextAsync(ct);
        return await (from job in db.AutomatedJobs.AsNoTracking()
                      join user in db.Users.IgnoreQueryFilters().AsNoTracking() on job.UserId equals user.Id
                      orderby job.Id descending
                      select new AutomatedJobModel
                      {
                          Id = job.Id, Name = job.Name, IterationMinutes = job.IterationMinutes,
                          SourceKey = job.SourceKey, UserId = job.UserId,
                          AuthorDisplayName = user.DisplayName,
                          SqlValue = job.SqlValue, IsEnabled = job.IsEnabled,
                          CreatedAtUtc = job.CreatedAtUtc, UpdatedAtUtc = job.UpdatedAtUtc,
                          LastCalledAtUtc = job.LastCalledAtUtc, LastScheduledAtUtc = job.LastScheduledAtUtc,
                          LastResult = job.LastResult
                      }).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<AutomatedJobRunModel>> RecentRunsAsync(long id, ClaimsPrincipal actor, CancellationToken ct = default)
    {
        if (!await CanView(actor, ct)) throw new UnauthorizedAccessException();
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.AutomatedJobRuns.AsNoTracking().Where(x => x.JobId == id)
            .OrderByDescending(x => x.Id).Take(75)
            // History is intentionally compact: read only timestamps, status and trigger.
            .Select(x => new AutomatedJobRunModel(x.Id, x.JobId, x.ScheduledAtUtc,
                x.Trigger, x.Status, x.StartedAtUtc, x.EndedAtUtc, null, null, null))
            .ToListAsync(ct);
    }

    public async Task<JobOperationResult> SaveAsync(AutomatedJobModel model, ClaimsPrincipal actor, CancellationToken ct = default)
    {
        if (!await CanManageAsync(actor, ct)) return JobOperationResult.Fail("مجوز مدیریت کارهای خودکار را ندارید.");
        var userId = ActorId(actor);
        if (userId == 0) return JobOperationResult.Fail("نشست کاربری معتبر نیست.");
        var name = (model.Name ?? "").Trim();
        if (name.Length is < 3 or > 180) return JobOperationResult.Fail("نام کار باید بین ۳ تا ۱۸۰ نویسه باشد.");
        if (model.IterationMinutes is < 1 or > 1440) return JobOperationResult.Fail("تناوب باید بین ۱ تا ۱۴۴۰ دقیقه باشد.");
        if (!sources.IsConfigured(model.SourceKey))
            return JobOperationResult.Fail("منبع داده در اتصالات مجاز کارهای خودکار تعریف نشده است.");
        if (model.IsEnabled && model.Id == 0 && string.IsNullOrWhiteSpace(model.SqlValue))
            return JobOperationResult.Fail("برای فعال‌کردن کار، ابتدا دستور SQL را ثبت کنید.");
        if (!string.IsNullOrWhiteSpace(model.SqlValue) && !MonitoringSqlPolicy.IsAllowed(model.SqlValue))
            return JobOperationResult.Fail("فقط یک دستور SELECT ایمن و خواندنی مجاز است.");
        await using var db = await factory.CreateDbContextAsync(ct);
        AutomatedJob job;
        if (model.Id == 0)
        {
            job = new AutomatedJob { UserId = userId, CreatedAtUtc = DateTimeOffset.UtcNow, LastResult = "هنوز اجرا نشده" };
            db.AutomatedJobs.Add(job);
        }
        else
        {
            job = await db.AutomatedJobs.FirstOrDefaultAsync(x => x.Id == model.Id, ct)
                ?? throw new KeyNotFoundException("کار پیدا نشد.");
            // The author is preserved; only a privileged manager can edit.
        }
        job.Name = name;
        job.SourceKey = model.SourceKey;
        job.IterationMinutes = model.IterationMinutes;
        if (model.IsEnabled && string.IsNullOrWhiteSpace(job.SqlValue) && model.Id != 0)
            return JobOperationResult.Fail("ابتدا SQL کار را از منوی مقدار کار ثبت کنید.");
        job.IsEnabled = model.IsEnabled;
        job.UpdatedAtUtc = DateTimeOffset.UtcNow;
        // Query is edited through the dedicated SQL modal; optional first-time value on create.
        if (model.Id == 0) job.SqlValue = model.SqlValue?.Trim() ?? "";
        await db.SaveChangesAsync(ct);
        return JobOperationResult.Ok(model.Id == 0 ? "کار جدید ایجاد شد." : "تنظیمات کار ذخیره شد.");
    }

    public async Task<JobOperationResult> UpdateSqlAsync(long id, string sql, ClaimsPrincipal actor, CancellationToken ct = default)
    {
        if (!await CanManageAsync(actor, ct)) return JobOperationResult.Fail("مجوز ویرایش SQL را ندارید.");
        if (!string.IsNullOrWhiteSpace(sql) && !MonitoringSqlPolicy.IsAllowed(sql))
            return JobOperationResult.Fail("فقط یک دستور SELECT بدون دستورات تغییردهنده مجاز است.");
        await using var db = await factory.CreateDbContextAsync(ct);
        var job = await db.AutomatedJobs.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (job is null) return JobOperationResult.Fail("کار پیدا نشد.");
        job.SqlValue = sql?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(job.SqlValue)) job.IsEnabled = false;
        job.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return JobOperationResult.Ok("دستور SQL ذخیره شد.");
    }

    public async Task<JobOperationResult> DeleteAsync(long id, ClaimsPrincipal actor, CancellationToken ct = default)
    {
        if (!await CanManageAsync(actor, ct)) return JobOperationResult.Fail("مجوز حذف کار را ندارید.");
        await using var db = await factory.CreateDbContextAsync(ct);
        var job = await db.AutomatedJobs.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (job is null) return JobOperationResult.Fail("کار پیدا نشد.");
        // A report must never lose its source job, even when the job is inactive.
        if (await db.MonitoringReports.AnyAsync(x => x.AutomatedJobId == id, ct))
            return JobOperationResult.Fail("این کار به یک یا چند گزارش متصل است. ابتدا گزارش‌های مرتبط را حذف یا به کار دیگری منتقل کنید.");
        job.IsEnabled = false;
        job.IsDeleted = true;
        job.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return JobOperationResult.Ok("کار غیرفعال و حذف شد.");
    }

    public async Task<JobOperationResult> TestAsync(long id, ClaimsPrincipal actor, CancellationToken ct = default)
    {
        if (!await CanTestAsync(actor, ct)) return JobOperationResult.Fail("مجوز اجرای آزمایشی را ندارید.");
        var (ok, message) = await runner.RunAsync(id, null, ct);
        return new JobOperationResult(ok, message);
    }
}
