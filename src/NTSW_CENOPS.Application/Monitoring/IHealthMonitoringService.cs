namespace NTSW_CENOPS.Application.Monitoring;

public interface IHealthMonitoringService
{
    Task PollDueApisAsync(CancellationToken cancellationToken = default);
}
