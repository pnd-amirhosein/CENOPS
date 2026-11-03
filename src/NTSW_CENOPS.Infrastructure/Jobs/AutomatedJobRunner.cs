using System.Collections.Concurrent;
using NTSW_CENOPS.Application.Jobs;
using NTSW_CENOPS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace NTSW_CENOPS.Infrastructure.Jobs;

/// <summary>One active execution per job; scheduled slots claimed uniquely in SQL.</summary>
public sealed class AutomatedJobRunner(
    IDbContextFactory<AppDbContext> factory, JobQueryExecutor executor,
    IAutomatedJobDataService dataService, IConfiguration config, ILogger<AutomatedJobRunner> logger)
{
    private static readonly ConcurrentDictionary<long, SemaphoreSlim> Locks = new();

    public async Task<(bool Success, string Message)> RunAsync(long id, DateTimeOffset? slotUtc,
        CancellationToken ct = default)
    {
        var gate = Locks.GetOrAdd(id, static _ => new SemaphoreSlim(1, 1));
        if (!await gate.WaitAsync(0, ct)) return (false, "این کار در حال اجرا است.");
        try
        {
            await using var db = await factory.CreateDbContextAsync(ct);
            var job = await db.AutomatedJobs.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (job is null || job.IsDeleted) return (false, "کار پیدا نشد.");
            // Guard even manually triggered runs before creating any history or touching a connection.
            if (string.IsNullOrWhiteSpace(job.SqlValue)) return (false, "این کار فاقد دستور SQL است؛ ابتدا دستور را ثبت کنید.");
            if (slotUtc.HasValue && !job.IsEnabled) return (false, "کار غیرفعال است.");
            if (slotUtc.HasValue && await db.AutomatedJobRuns.AnyAsync(x => x.JobId == id && x.ScheduledAtUtc == slotUtc.Value, ct))
                return (false, "این نوبت قبلاً ثبت شده است.");
            var now = DateTimeOffset.UtcNow;
            var run = new AutomatedJobRun
            {
                JobId = id, ScheduledAtUtc = slotUtc, Trigger = slotUtc.HasValue ? "Scheduled" : "Manual",
                StartedAtUtc = now, Status = "Running"
            };
            db.AutomatedJobRuns.Add(run);
            job.LastCalledAtUtc = now;
            job.LastResult = "در حال اجرا";
            if (slotUtc.HasValue) job.LastScheduledAtUtc = slotUtc.Value;
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateException) when (slotUtc.HasValue) { return (false, "این نوبت توسط اجرای دیگری ثبت شده است."); }
            try
            {
                // Scheduled runs write the preceding interval; manual tests write the
                // preceding interval relative to their actual execution time.
                var windowEndUtc = slotUtc ?? now;
                var windowStartUtc = windowEndUtc.AddMinutes(-job.IterationMinutes);
                var result = await executor.ExecuteAsync(job, windowStartUtc, windowEndUtc, ct);

                // Group by the monitored window's START date in the configured timezone.
                // A run at local midnight therefore updates yesterday's daily document.
                var zone = TimeZoneInfo.FindSystemTimeZoneById(config["Jobs:TimeZoneId"] ?? "Asia/Tehran");
                var day = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(windowStartUtc, zone).DateTime);
                // Save first. A job must never be marked successful if its data was lost.
                await dataService.UpsertWindowAsync(id, day, windowStartUtc, windowEndUtc, result.RowsJson, ct);
                run.RowsRead = result.RowsRead;
                run.ResultPreview = result.Summary;
                run.Status = "Succeeded";
                job.LastResult = "موفق";
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                run.Status = "Cancelled";
                run.Error = "Execution was cancelled.";
                job.LastResult = "لغو شد";
                throw;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Automated job {JobId} failed", id);
                run.Status = "Failed";
                run.Error = ex.Message[..Math.Min(ex.Message.Length, 1100)];
                job.LastResult = "ناموفق";
            }
            finally
            {
                run.EndedAtUtc = DateTimeOffset.UtcNow;
                // Give shutdown a chance to persist failure/cancellation state if the caller was cancelled.
                await db.SaveChangesAsync(CancellationToken.None);
            }
            return (run.Status == "Succeeded", run.Status == "Succeeded" ? (slotUtc.HasValue ? "اجرای زمان‌بندی‌شده موفق بود." : "اجرای آزمایشی موفق بود.") : "اجرای کار ناموفق بود: " + run.Error);
        }
        finally { gate.Release(); }
    }
}
