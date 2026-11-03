using NTSW_CENOPS.Application.Monitoring;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace NTSW_CENOPS.Infrastructure.Monitoring;

public sealed class HealthPollingWorker : BackgroundService
{
    private static readonly TimeSpan PollingTick = TimeSpan.FromMinutes(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<HealthPollingWorker> _logger;

    public HealthPollingWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<HealthPollingWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var monitoringService = scope.ServiceProvider.GetRequiredService<IHealthMonitoringService>();
                await monitoringService.PollDueApisAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in health polling loop");
            }

            try
            {
                await Task.Delay(PollingTick, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
