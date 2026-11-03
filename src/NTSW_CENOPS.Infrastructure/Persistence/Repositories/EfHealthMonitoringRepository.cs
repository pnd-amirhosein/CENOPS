using NTSW_CENOPS.Application.Monitoring;
using NTSW_CENOPS.Domain.Monitoring;
using Microsoft.EntityFrameworkCore;

namespace NTSW_CENOPS.Infrastructure.Persistence.Repositories;

public sealed class EfHealthMonitoringRepository : IHealthMonitoringRepository
{
    private readonly AppDbContext _dbContext;

    public EfHealthMonitoringRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<HealthApi>> GetActiveApisAsync(
        CancellationToken cancellationToken = default) =>
        await _dbContext.HealthApis
            .AsNoTracking()
            .Where(x => x.IsActive)
            .ToListAsync(cancellationToken);

    public Task<DateTimeOffset?> GetLastCheckedAtAsync(
        int healthApiId,
        CancellationToken cancellationToken = default) =>
        _dbContext.HealthCheckResults
            .AsNoTracking()
            .Where(x => x.HealthApiId == healthApiId)
            .OrderByDescending(x => x.CheckedAt)
            .Select(x => (DateTimeOffset?)x.CheckedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task AddResultAsync(
        HealthCheckResult result,
        CancellationToken cancellationToken = default)
    {
        _dbContext.HealthCheckResults.Add(result);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
