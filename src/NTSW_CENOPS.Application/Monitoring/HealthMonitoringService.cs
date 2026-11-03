using NTSW_CENOPS.Domain.Monitoring;
using Microsoft.Extensions.Logging;

namespace NTSW_CENOPS.Application.Monitoring;

public sealed class HealthMonitoringService : IHealthMonitoringService
{
    private readonly IHealthMonitoringRepository _repository;
    private readonly IHealthProbe _healthProbe;
    private readonly ILogger<HealthMonitoringService> _logger;

    public HealthMonitoringService(
        IHealthMonitoringRepository repository,
        IHealthProbe healthProbe,
        ILogger<HealthMonitoringService> logger)
    {
        _repository = repository;
        _healthProbe = healthProbe;
        _logger = logger;
    }

    public async Task PollDueApisAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var apis = await _repository.GetActiveApisAsync(cancellationToken);

        foreach (var api in apis)
        {
            var lastCheckedAt = await _repository.GetLastCheckedAtAsync(api.Id, cancellationToken);
            var interval = TimeSpan.FromMinutes(Math.Max(1, api.IntervalMinutes));
            var isDue = lastCheckedAt is null || now - lastCheckedAt.Value >= interval;

            if (!isDue)
            {
                continue;
            }

            var probe = await _healthProbe.CheckAsync(api, cancellationToken);

            await _repository.AddResultAsync(
                new HealthCheckResult
                {
                    HealthApiId = api.Id,
                    CheckedAt = DateTimeOffset.UtcNow,
                    IsSuccess = probe.IsSuccess,
                    StatusCode = probe.StatusCode,
                    DurationMs = probe.DurationMs,
                    ErrorMessage = probe.ErrorMessage
                },
                cancellationToken);

            _logger.LogInformation(
                "HealthCheck {ApiName} => Success={Success}, Status={StatusCode}, DurationMs={DurationMs}",
                api.Name,
                probe.IsSuccess,
                probe.StatusCode,
                probe.DurationMs);
        }
    }
}
