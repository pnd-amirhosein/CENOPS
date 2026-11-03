using NTSW_CENOPS.Domain.Authorization;
using NTSW_CENOPS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace NTSW_CENOPS.Infrastructure.Authorization;

/// <summary>Grants initial permissions once, when a built-in role is first created after migration.</summary>
public static class DefaultRoleAccessSeeder
{
    public static async Task GrantOnNewRoleAsync(AppDbContext db, int roleId, string? roleName, CancellationToken ct = default)
    {
        var keys = new List<string>();
        if (string.Equals(roleName, RoleNames.User, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(roleName, RoleNames.Admin, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(roleName, RoleNames.Owner, StringComparison.OrdinalIgnoreCase))
        {
            keys.AddRange(new[] { AccessKeys.Dashboard, AccessKeys.DatabaseInspector, AccessKeys.Events, AccessKeys.Contacts,
                AccessKeys.HealthApis, AccessKeys.Curls, AccessKeys.SqlQueries, AccessKeys.Notes, AccessKeys.Todos, AccessKeys.Announcements, AccessKeys.Chat, AccessKeys.Weather, AccessKeys.ViewReports, AccessKeys.ManageReports });
        }
        if (string.Equals(roleName, RoleNames.Admin, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(roleName, RoleNames.Owner, StringComparison.OrdinalIgnoreCase))
            keys.AddRange(new[] { AccessKeys.SourceIntelligence, AccessKeys.Settings, AccessKeys.ViewJobs, AccessKeys.ManageJobs, AccessKeys.ExecuteJobs, AccessKeys.ViewReports, AccessKeys.ManageReports });
        if (string.Equals(roleName, RoleNames.Owner, StringComparison.OrdinalIgnoreCase))
            keys.Add(AccessKeys.ManageAccess);
        if (keys.Count == 0) return;

        var ids = await db.Accesses.AsNoTracking().Where(x => keys.Contains(x.Key))
            .Select(x => x.Id).ToListAsync(ct);
        var current = await db.RoleAccesses.AsNoTracking().Where(x => x.RoleId == roleId)
            .Select(x => x.AccessId).ToListAsync(ct);
        db.RoleAccesses.AddRange(ids.Except(current).Select(id => new RoleAccess
        {
            RoleId = roleId,
            AccessId = id
        }));
        await db.SaveChangesAsync(ct);
    }
}
