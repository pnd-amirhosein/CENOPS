using System.Security.Claims;
using NTSW_CENOPS.Application.Access;
using NTSW_CENOPS.Application.Knowledge;
using NTSW_CENOPS.Domain.Authorization;
using NTSW_CENOPS.Infrastructure.Identity;
using NTSW_CENOPS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace NTSW_CENOPS.Infrastructure.Knowledge;

public abstract class KnowledgeServiceBase<T>(IDbContextFactory<AppDbContext> factory, IAccessService accessService, string accessKey)
    : IKnowledgeService where T : KnowledgeEntryBase, new()
{
    private async Task<bool> AllowedAsync(ClaimsPrincipal actor, CancellationToken ct) =>
        await accessService.HasAccessAsync(actor, accessKey, ct);

    private static int ActorId(ClaimsPrincipal actor) =>
        int.TryParse(actor.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;

    private static async Task<bool> IsPrivilegedAsync(AppDbContext db, int actorId, CancellationToken ct) =>
        await (from membership in db.UserRoles
               join role in db.Roles on membership.RoleId equals role.Id
               where membership.UserId == actorId &&
                     (role.Name == RoleNames.Owner || role.Name == RoleNames.Admin)
               select role.Id).AnyAsync(ct);

    public async Task<bool> CanChooseAuthorAsync(ClaimsPrincipal actor, CancellationToken ct = default)
    {
        if (!await AllowedAsync(actor, ct)) return false;
        await using var db = await factory.CreateDbContextAsync(ct);
        return await IsPrivilegedAsync(db, ActorId(actor), ct);
    }

    public async Task<IReadOnlyList<KnowledgeAuthorModel>> AuthorsAsync(ClaimsPrincipal actor, CancellationToken ct = default)
    {
        if (!await AllowedAsync(actor, ct)) return [];
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Users.AsNoTracking().OrderBy(x => x.DisplayName)
            .Select(x => new KnowledgeAuthorModel(x.Id, x.UserName ?? "", x.DisplayName))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<KnowledgeItemModel>> ListAsync(ClaimsPrincipal actor, CancellationToken ct = default)
    {
        if (!await AllowedAsync(actor, ct)) return [];
        await using var db = await factory.CreateDbContextAsync(ct);
        var actorId = ActorId(actor);
        var privileged = await IsPrivilegedAsync(db, actorId, ct);
        return await (from item in db.Set<T>().AsNoTracking()
                      where !item.IsDeleted && (item.Visibility == KnowledgeVisibility.Public || item.UserId == actorId || privileged)
                      join user in db.Users.IgnoreQueryFilters().AsNoTracking() on item.UserId equals user.Id into authors
                      from author in authors.DefaultIfEmpty()
                      orderby item.Id descending
                      select new KnowledgeItemModel
                      {
                          Id = item.Id, Title = item.Title, Name = item.Name, Description = item.Description, Visibility = item.Visibility,
                          UserId = item.UserId, AuthorDisplayName = author != null ? author.DisplayName : "کاربر حذف‌شده",
                          AuthorUsername = author != null ? author.UserName ?? "" : ""
                      }).ToListAsync(ct);
    }

    public async Task<KnowledgeItemModel?> FindAsync(int id, ClaimsPrincipal actor, CancellationToken ct = default)
    {
        if (!await AllowedAsync(actor, ct) && !await accessService.HasAccessAsync(actor, AccessKeys.Dashboard, ct)) return null;
        await using var db = await factory.CreateDbContextAsync(ct);
        var actorId = ActorId(actor);
        var privileged = await IsPrivilegedAsync(db, actorId, ct);
        return await (from item in db.Set<T>().AsNoTracking()
                      where !item.IsDeleted && (item.Visibility == KnowledgeVisibility.Public || item.UserId == actorId || privileged)
                      join user in db.Users.IgnoreQueryFilters().AsNoTracking() on item.UserId equals user.Id into authors
                      from author in authors.DefaultIfEmpty()
                      where item.Id == id && !item.IsDeleted
                      select new KnowledgeItemModel
                      {
                          Id = item.Id, Title = item.Title, Name = item.Name, Description = item.Description, Visibility = item.Visibility,
                          Value = item.Value, UserId = item.UserId,
                          AuthorDisplayName = author != null ? author.DisplayName : "کاربر حذف‌شده",
                          AuthorUsername = author != null ? author.UserName ?? "" : ""
                      }).FirstOrDefaultAsync(ct);
    }

    public async Task<bool> CanManageAsync(KnowledgeItemModel item, ClaimsPrincipal actor, CancellationToken ct = default)
    {
        if (!await AllowedAsync(actor, ct)) return false;
        var actorId = ActorId(actor);
        if (actorId == 0) return false;
        await using var db = await factory.CreateDbContextAsync(ct);
        var actualUserId = await db.Set<T>().AsNoTracking().Where(x => x.Id == item.Id && !x.IsDeleted)
            .Select(x => (int?)x.UserId).FirstOrDefaultAsync(ct);
        return actualUserId.HasValue && (actualUserId == actorId || await IsPrivilegedAsync(db, actorId, ct));
    }

    public async Task<KnowledgeSaveResult> SaveAsync(KnowledgeItemModel item, ClaimsPrincipal actor, CancellationToken ct = default)
    {
        if (!await AllowedAsync(actor, ct)) return KnowledgeSaveResult.Fail("شما به این بخش دسترسی ندارید.");
        var actorId = ActorId(actor);
        if (actorId == 0) return KnowledgeSaveResult.Fail("لطفاً دوباره وارد شوید.");
        var title = (item.Title ?? "").Trim();
        var description = (item.Description ?? "").Trim();
        var value = (item.Value ?? "").Trim();
        if (title.Length is < 3 or > 250) return KnowledgeSaveResult.Fail("عنوان باید بین ۳ تا ۲۵۰ نویسه باشد.");
        if (description.Length > 4000) return KnowledgeSaveResult.Fail("توضیحات طولانی‌تر از حد مجاز است.");
        if (value.Length == 0) return KnowledgeSaveResult.Fail("وارد کردن کد الزامی است.");
        if (value.Length > 100_000) return KnowledgeSaveResult.Fail("طول کد از حد مجاز بیشتر است.");

        await using var db = await factory.CreateDbContextAsync(ct);
        var privileged = await IsPrivilegedAsync(db, actorId, ct);
        var selectedAuthor = item.UserId == 0 ? actorId : item.UserId;
        if (!privileged && selectedAuthor != actorId)
            return KnowledgeSaveResult.Fail("فقط مدیر می‌تواند نویسنده دیگری انتخاب کند.");
        if (!await db.Users.AnyAsync(x => x.Id == selectedAuthor, ct))
            return KnowledgeSaveResult.Fail("نویسنده انتخاب‌شده معتبر نیست.");

        T entity;
        if (item.Id == 0)
        {
            entity = new T();
            db.Set<T>().Add(entity);
        }
        else
        {
            entity = await db.Set<T>().FirstOrDefaultAsync(x => x.Id == item.Id && !x.IsDeleted, ct)
                     ?? throw new KeyNotFoundException("مورد درخواستی پیدا نشد.");
            if (entity.UserId != actorId && !privileged)
                return KnowledgeSaveResult.Fail("فقط نویسنده یا مدیر می‌تواند این مورد را ویرایش کند.");
        }
        entity.Title = title;
        entity.Name = title; // A consistent Name alias without asking for duplicate user input.
        entity.Description = description;
        entity.Value = value;
        entity.UserId = selectedAuthor;
        entity.Visibility = item.Visibility;
        await db.SaveChangesAsync(ct);
        return KnowledgeSaveResult.Success(entity.Id);
    }

    public async Task<KnowledgeSaveResult> DeleteAsync(int id, ClaimsPrincipal actor, CancellationToken ct = default)
    {
        if (!await AllowedAsync(actor, ct)) return KnowledgeSaveResult.Fail("شما به این بخش دسترسی ندارید.");
        var actorId = ActorId(actor);
        await using var db = await factory.CreateDbContextAsync(ct);
        var entity = await db.Set<T>().FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct);
        if (entity is null) return KnowledgeSaveResult.Fail("مورد درخواستی پیدا نشد.");
        if (entity.UserId != actorId && !await IsPrivilegedAsync(db, actorId, ct))
            return KnowledgeSaveResult.Fail("اجازه حذف این مورد را ندارید.");
        entity.IsDeleted = true;
        await db.SaveChangesAsync(ct);
        return KnowledgeSaveResult.Success(id);
    }
}

public sealed class CurlService(IDbContextFactory<AppDbContext> factory, IAccessService access)
    : KnowledgeServiceBase<Curl>(factory, access, AccessKeys.Curls), ICurlService { }

public sealed class SqlQueryService(IDbContextFactory<AppDbContext> factory, IAccessService access)
    : KnowledgeServiceBase<SqlQuery>(factory, access, AccessKeys.SqlQueries), ISqlQueryService { }
