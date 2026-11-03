using NTSW_CENOPS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace NTSW_CENOPS.Infrastructure.Jobs;

public sealed class AutomatedJobWorker(IServiceScopeFactory scopes, IConfiguration config,
    ILogger<AutomatedJobWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        TimeZoneInfo zone;
        try { zone = TimeZoneInfo.FindSystemTimeZoneById(config["Jobs:TimeZoneId"] ?? "Asia/Tehran"); }
        catch (TimeZoneNotFoundException) { logger.LogError("Invalid Jobs:TimeZoneId; scheduler disabled"); return; }
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
        try
        {
            do
            {
                try
                {
                    using var scope = scopes.CreateScope();
                    var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
                    var runner = scope.ServiceProvider.GetRequiredService<AutomatedJobRunner>();
                    await using var db = await factory.CreateDbContextAsync(stoppingToken);
                    var jobs = await db.AutomatedJobs.AsNoTracking().Where(x => x.IsEnabled && !x.IsDeleted && x.SqlValue != "").ToListAsync(stoppingToken);
                    var now = DateTimeOffset.UtcNow;
                    var dueJobs = jobs.Select(job => (job, due: JobSchedule.DueSlotUtc(now, job.IterationMinutes, zone)))
                        .Where(x => x.due is not null && x.due <= now && (now - x.due.Value).TotalSeconds <= 90
                                    && x.job.LastScheduledAtUtc != x.due)
                        .ToList();
                    var parallelism = Math.Clamp(config.GetValue<int?>("Jobs:MaxConcurrent") ?? 3, 1, 8);
                    using var throttle = new SemaphoreSlim(parallelism, parallelism);
                    await Task.WhenAll(dueJobs.Select(async entry =>
                    {
                        await throttle.WaitAsync(stoppingToken);
                        try { await runner.RunAsync(entry.job.Id, entry.due!.Value, stoppingToken); }
                        finally { throttle.Release(); }
                    }));
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception ex) { logger.LogError(ex, "Automated job scheduler iteration failed"); }
            } while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
