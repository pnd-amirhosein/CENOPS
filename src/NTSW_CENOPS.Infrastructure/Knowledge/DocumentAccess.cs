using System.Security.Claims;
using NTSW_CENOPS.Application.Knowledge;
using NTSW_CENOPS.Domain.Authorization;
using NTSW_CENOPS.Infrastructure.Identity;
using NTSW_CENOPS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace NTSW_CENOPS.Infrastructure.Knowledge;

/// <summary>Server-side role/ownership checks shared by todo and announcement services.</summary>
internal static class DocumentAccess
{
    public static int ActorId(ClaimsPrincipal actor) =>
        int.TryParse(actor.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;

    public static Task<bool> IsManagerAsync(AppDbContext db, int actorId, CancellationToken ct) =>
        db.UserRoles.Join(db.Roles, ur => ur.RoleId, role => role.Id, (ur, role) => new { ur, role })
            .AnyAsync(x => x.ur.UserId == actorId &&
                (x.role.Name == RoleNames.Owner || x.role.Name == RoleNames.Admin), ct);

    public static Task<List<KnowledgeAuthorModel>> AuthorsAsync(AppDbContext db, CancellationToken ct) =>
        db.Users.AsNoTracking().OrderBy(x => x.DisplayName)
            .Select(x => new KnowledgeAuthorModel(x.Id, x.UserName ?? "", x.DisplayName)).ToListAsync(ct);
}
