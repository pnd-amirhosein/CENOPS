using System.Security.Claims;
using NTSW_CENOPS.Application.Access;
using NTSW_CENOPS.Application.Administration;
using NTSW_CENOPS.Domain.Authorization;
using NTSW_CENOPS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace NTSW_CENOPS.Infrastructure.Authorization;

public sealed class AccessService(IDbContextFactory<AppDbContext> factory) : IAccessService
{
    private static readonly HashSet<string> SystemKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        AccessKeys.Dashboard, AccessKeys.SourceIntelligence, AccessKeys.DatabaseInspector, AccessKeys.Events, AccessKeys.Contacts, AccessKeys.HealthApis,
        AccessKeys.Curls, AccessKeys.SqlQueries, AccessKeys.Notes, AccessKeys.Chat, AccessKeys.Weather, AccessKeys.Settings, AccessKeys.ViewJobs, AccessKeys.ManageJobs, AccessKeys.ExecuteJobs, AccessKeys.ManageAccess, AccessKeys.ViewReports, AccessKeys.ManageReports
    };

    public async Task<bool> HasAccessAsync(ClaimsPrincipal actor, string key, CancellationToken ct = default)
    {
        if (!TryGetId(actor, out var userId) || string.IsNullOrWhiteSpace(key)) return false;
        await using var db = await factory.CreateDbContextAsync(ct);
        var normalized = key.Trim().ToLowerInvariant();
        // Live database role membership, not potentially stale cookie role claims.
        return await (from user in db.Users.AsNoTracking()
                      join membership in db.UserRoles.AsNoTracking() on user.Id equals membership.UserId
                      join role in db.Roles.AsNoTracking() on membership.RoleId equals role.Id
                      join grant in db.RoleAccesses.AsNoTracking() on role.Id equals grant.RoleId
                      join access in db.Accesses.AsNoTracking() on grant.AccessId equals access.Id
                      where user.Id == userId && access.Key == normalized
                      select grant.AccessId).AnyAsync(ct);
    }

    public async Task<IReadOnlySet<string>> GetCurrentAccessKeysAsync(ClaimsPrincipal actor, CancellationToken ct = default)
    {
        if (!TryGetId(actor, out var userId)) return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var db = await factory.CreateDbContextAsync(ct);
        var keys = await (from user in db.Users.AsNoTracking()
                          join membership in db.UserRoles.AsNoTracking() on user.Id equals membership.UserId
                          join role in db.Roles.AsNoTracking() on membership.RoleId equals role.Id
                          join grant in db.RoleAccesses.AsNoTracking() on role.Id equals grant.RoleId
                          join access in db.Accesses.AsNoTracking() on grant.AccessId equals access.Id
                          where user.Id == userId
                          select access.Key).Distinct().ToListAsync(ct);
        return keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public async Task<IReadOnlyList<AccessModel>> GetAllAsync(ClaimsPrincipal actor, CancellationToken ct = default)
    {
        await AssertOwnerAsync(actor, ct);
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Accesses.AsNoTracking().OrderBy(x => x.Name)
            .Select(x => new AccessModel { Id = x.Id, Key = x.Key, Name = x.Name, Description = x.Description })
            .ToListAsync(ct);
    }

    public async Task<AdminOperationResult> CreateAsync(AccessModel model, ClaimsPrincipal actor, CancellationToken ct = default)
    {
        if (!await IsOwnerAsync(actor, ct)) return Denied();
        await using var db = await factory.CreateDbContextAsync(ct);
        var key = model.Key.Trim().ToLowerInvariant();
        var name = model.Name.Trim();
        if (key.Length is < 3 or > 120 || !key.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_') || string.IsNullOrWhiteSpace(name) || name.Length > 150)
            return AdminOperationResult.Failure("کد دسترسی باید ۳ تا ۱۲۰ نویسه انگلیسی، عدد، نقطه یا خط تیره باشد و عنوان الزامی است.");
        if (await db.Accesses.AnyAsync(x => x.Key == key, ct))
            return AdminOperationResult.Failure("این کد دسترسی از قبل وجود دارد.");
        db.Accesses.Add(new Access { Key = key, Name = name, Description = (model.Description ?? string.Empty).Trim() });
        try { await db.SaveChangesAsync(ct); return AdminOperationResult.Success(); }
        catch (DbUpdateException) { db.ChangeTracker.Clear(); return AdminOperationResult.Failure("ثبت دسترسی انجام نشد. کد را بررسی کنید."); }
    }

    public async Task<AdminOperationResult> UpdateAsync(AccessModel model, ClaimsPrincipal actor, CancellationToken ct = default)
    {
        if (!await IsOwnerAsync(actor, ct)) return Denied();
        await using var db = await factory.CreateDbContextAsync(ct);
        var entity = await db.Accesses.FindAsync(new object[] { model.Id }, ct);
        if (entity is null) return AdminOperationResult.Failure("دسترسی پیدا نشد.");
        var key = model.Key.Trim().ToLowerInvariant();
        var name = model.Name.Trim();
        if (key.Length is < 3 or > 120 || !key.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_') || string.IsNullOrWhiteSpace(name) || name.Length > 150)
            return AdminOperationResult.Failure("کد یا عنوان دسترسی معتبر نیست.");
        if (SystemKeys.Contains(entity.Key) && key != entity.Key)
            return AdminOperationResult.Failure("کد دسترسی سیستمی قابل تغییر نیست.");
        if (await db.Accesses.AnyAsync(x => x.Id != model.Id && x.Key == key, ct))
            return AdminOperationResult.Failure("این کد دسترسی از قبل وجود دارد.");
        entity.Key = key;
        entity.Name = name;
        entity.Description = (model.Description ?? string.Empty).Trim();
        try { await db.SaveChangesAsync(ct); return AdminOperationResult.Success(); }
        catch (DbUpdateException) { db.ChangeTracker.Clear(); return AdminOperationResult.Failure("به‌روزرسانی دسترسی انجام نشد."); }
    }

    public async Task<AdminOperationResult> DeleteAsync(int id, ClaimsPrincipal actor, CancellationToken ct = default)
    {
        if (!await IsOwnerAsync(actor, ct)) return Denied();
        await using var db = await factory.CreateDbContextAsync(ct);
        var entity = await db.Accesses.FindAsync(new object[] { id }, ct);
        if (entity is null) return AdminOperationResult.Failure("دسترسی پیدا نشد.");
        if (SystemKeys.Contains(entity.Key)) return AdminOperationResult.Failure("دسترسی پایه سامانه قابل حذف نیست.");
        if (await db.RoleAccesses.AnyAsync(x => x.AccessId == id, ct))
            return AdminOperationResult.Failure("این دسترسی به نقش اختصاص داده شده است. ابتدا اتصال نقش‌ها را حذف کنید.");
        entity.IsDeleted = true;
        await db.SaveChangesAsync(ct);
        return AdminOperationResult.Success();
    }

    // Reverse side of the N:N relationship: one Access -> many active roles.
    public async Task<IReadOnlySet<int>> GetAccessRoleIdsAsync(int accessId, ClaimsPrincipal actor, CancellationToken ct = default)
    {
        await AssertOwnerAsync(actor, ct);
        await using var db = await factory.CreateDbContextAsync(ct);
        if (!await db.Accesses.AnyAsync(x => x.Id == accessId, ct))
            throw new InvalidOperationException("دسترسی موردنظر پیدا نشد.");
        return (await (from link in db.RoleAccesses.AsNoTracking()
                       join role in db.Roles.AsNoTracking() on link.RoleId equals role.Id
                       where link.AccessId == accessId
                       select link.RoleId).ToListAsync(ct)).ToHashSet();
    }

    public async Task<AdminOperationResult> UpdateAccessRolesAsync(
        int accessId, IReadOnlyCollection<int> roleIds, ClaimsPrincipal actor, CancellationToken ct = default)
    {
        if (!await IsOwnerAsync(actor, ct)) return Denied();
        if (roleIds is null) return AdminOperationResult.Failure("فهرست نقش‌ها معتبر نیست.");
        await using var db = await factory.CreateDbContextAsync(ct);
        var access = await db.Accesses.AsNoTracking().FirstOrDefaultAsync(x => x.Id == accessId, ct);
        if (access is null) return AdminOperationResult.Failure("دسترسی موردنظر پیدا نشد.");

        var desired = roleIds.ToHashSet();
        var validIds = await db.Roles.AsNoTracking().Where(x => desired.Contains(x.Id))
            .Select(x => x.Id).ToListAsync(ct);
        if (validIds.Count != desired.Count)
            return AdminOperationResult.Failure("یک یا چند نقش معتبر نیست. فهرست نقش‌ها را تازه‌سازی کنید.");

        // The Owner must keep its recovery permissions, regardless of which side edits the N:N link.
        if (access.Key is AccessKeys.Settings or AccessKeys.ManageAccess)
        {
            var ownerId = await db.Roles.AsNoTracking()
                .Where(x => x.NormalizedName == "OWNER")
                .Select(x => (int?)x.Id).FirstOrDefaultAsync(ct);
            if (ownerId.HasValue && !desired.Contains(ownerId.Value))
                return AdminOperationResult.Failure("نقش Owner باید دسترسی مدیریت سیستم و مدیریت دسترسی‌ها را حفظ کند.");
        }

        // Only mutate memberships of active roles; soft-deleted role links are left untouched.
        var current = await (from link in db.RoleAccesses
                             join role in db.Roles on link.RoleId equals role.Id
                             where link.AccessId == accessId
                             select link).ToListAsync(ct);
        var existing = current.Select(x => x.RoleId).ToHashSet();
        db.RoleAccesses.RemoveRange(current.Where(x => !desired.Contains(x.RoleId)));
        db.RoleAccesses.AddRange(desired.Except(existing)
            .Select(id => new RoleAccess { RoleId = id, AccessId = accessId }));
        try
        {
            await db.SaveChangesAsync(ct);
            return AdminOperationResult.Success();
        }
        catch (DbUpdateException)
        {
            return AdminOperationResult.Failure("ذخیره نقش‌های دسترسی انجام نشد. دوباره تلاش کنید.");
        }
    }

    public async Task<IReadOnlySet<int>> GetRoleAccessIdsAsync(int roleId, ClaimsPrincipal actor, CancellationToken ct = default)
    {
        await AssertOwnerAsync(actor, ct);
        await using var db = await factory.CreateDbContextAsync(ct);
        return (await (from grant in db.RoleAccesses.AsNoTracking()
                       join access in db.Accesses.AsNoTracking() on grant.AccessId equals access.Id
                       join role in db.Roles.AsNoTracking() on grant.RoleId equals role.Id
                       where grant.RoleId == roleId
                       select grant.AccessId).ToListAsync(ct)).ToHashSet();
    }

    public async Task<AdminOperationResult> UpdateRoleAccessesAsync(int roleId, IReadOnlyCollection<int> accessIds, ClaimsPrincipal actor, CancellationToken ct = default)
    {
        if (!await IsOwnerAsync(actor, ct)) return Denied();
        await using var db = await factory.CreateDbContextAsync(ct);
        if (!await db.Roles.AnyAsync(x => x.Id == roleId, ct))
            return AdminOperationResult.Failure("نقش موردنظر وجود ندارد.");
        var desired = accessIds.ToHashSet();
        var validIds = await db.Accesses.Where(x => desired.Contains(x.Id)).Select(x => x.Id).ToListAsync(ct);
        if (validIds.Count != desired.Count) return AdminOperationResult.Failure("یک یا چند دسترسی معتبر نیست.");
        var current = await db.RoleAccesses.Where(x => x.RoleId == roleId).ToListAsync(ct);
        var role = await db.Roles.AsNoTracking().FirstAsync(x => x.Id == roleId, ct);
        // Protect recovery: Owner must retain access to the settings and permission manager.
        if (string.Equals(role.Name, RoleNames.Owner, StringComparison.OrdinalIgnoreCase))
        {
            var mustKeep = await db.Accesses.Where(x => x.Key == AccessKeys.Settings || x.Key == AccessKeys.ManageAccess)
                .Select(x => x.Id).ToListAsync(ct);
            if (mustKeep.Any(id => !desired.Contains(id)))
                return AdminOperationResult.Failure("نقش Owner باید دسترسی مدیریت سیستم و مدیریت دسترسی‌ها را حفظ کند.");
        }
        db.RoleAccesses.RemoveRange(current.Where(x => !desired.Contains(x.AccessId)));
        var existing = current.Select(x => x.AccessId).ToHashSet();
        db.RoleAccesses.AddRange(desired.Except(existing).Select(id => new RoleAccess { RoleId = roleId, AccessId = id }));
        await db.SaveChangesAsync(ct);
        return AdminOperationResult.Success();
    }

    private static bool TryGetId(ClaimsPrincipal actor, out int id)
    {
        id = 0;
        return actor.Identity?.IsAuthenticated == true &&
            int.TryParse(actor.FindFirstValue(ClaimTypes.NameIdentifier), out id);
    }

    private async Task<bool> IsOwnerAsync(ClaimsPrincipal actor, CancellationToken ct)
    {
        if (!TryGetId(actor, out var id)) return false;
        await using var db = await factory.CreateDbContextAsync(ct);
        return await (from user in db.Users.AsNoTracking()
                      join membership in db.UserRoles.AsNoTracking() on user.Id equals membership.UserId
                      join role in db.Roles.AsNoTracking() on membership.RoleId equals role.Id
                      where user.Id == id && role.NormalizedName == "OWNER"
                      select role.Id).AnyAsync(ct)
            && await HasAccessAsync(actor, AccessKeys.ManageAccess, ct);
    }

    private async Task AssertOwnerAsync(ClaimsPrincipal actor, CancellationToken ct)
    {
        if (!await IsOwnerAsync(actor, ct)) throw new UnauthorizedAccessException("دسترسی مدیریت مجوزها فقط برای مالک سامانه مجاز است.");
    }

    private static AdminOperationResult Denied() => AdminOperationResult.Failure("شما مجوز مدیریت دسترسی‌ها را ندارید.");
}
