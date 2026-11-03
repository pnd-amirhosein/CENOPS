using NTSW_CENOPS.Domain.Monitoring;

namespace NTSW_CENOPS.Application.Monitoring;

public interface IHealthProbe
{
    Task<HealthProbeResult> CheckAsync(
        HealthApi api,
        CancellationToken cancellationToken = default);
}
