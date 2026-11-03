using NTSW_CENOPS.Domain.Monitoring;

namespace NTSW_CENOPS.Application.Monitoring;

public interface IHealthMonitoringRepository
{
    Task<IReadOnlyList<HealthApi>> GetActiveApisAsync(CancellationToken cancellationToken = default);

    Task<DateTimeOffset?> GetLastCheckedAtAsync(
        int healthApiId,
        CancellationToken cancellationToken = default);

    Task AddResultAsync(
        HealthCheckResult result,
        CancellationToken cancellationToken = default);
}
