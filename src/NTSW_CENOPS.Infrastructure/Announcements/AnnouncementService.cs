using System.Security.Claims;
using NTSW_CENOPS.Application.Access;
using NTSW_CENOPS.Application.Announcements;
using NTSW_CENOPS.Application.Knowledge;
using NTSW_CENOPS.Domain.Authorization;
using NTSW_CENOPS.Infrastructure.Knowledge;
using NTSW_CENOPS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace NTSW_CENOPS.Infrastructure.Announcements;

public sealed class AnnouncementService(IDbContextFactory<AppDbContext> factory, IAccessService access) : IAnnouncementService
{
    private Task<bool> Allowed(ClaimsPrincipal actor, CancellationToken ct) =>
        access.HasAccessAsync(actor, AccessKeys.Announcements, ct);

    public async Task<bool> CanChooseAuthorAsync(ClaimsPrincipal actor, CancellationToken ct = default)
    {
        if (!await Allowed(actor, ct)) return false;
        await using var db = await factory.CreateDbContextAsync(ct);
        return await DocumentAccess.IsManagerAsync(db, DocumentAccess.ActorId(actor), ct);
    }

    public async Task<IReadOnlyList<KnowledgeAuthorModel>> AuthorsAsync(ClaimsPrincipal actor, CancellationToken ct = default)
    {
        if (!await Allowed(actor, ct)) return [];
        await using var db = await factory.CreateDbContextAsync(ct);
        return await DocumentAccess.AuthorsAsync(db, ct);
    }

    public async Task<IReadOnlyList<AnnouncementModel>> ListAsync(ClaimsPrincipal actor, CancellationToken ct = default)
    {
        if (!await Allowed(actor, ct)) return [];
        await using var db = await factory.CreateDbContextAsync(ct);
        var actorId = DocumentAccess.ActorId(actor);
        var manager = await DocumentAccess.IsManagerAsync(db, actorId, ct);
        return await (from item in db.Announcements.AsNoTracking()
                      join author in db.Users.IgnoreQueryFilters().AsNoTracking() on item.UserId equals author.Id
                      where !item.IsDeleted && (item.Visibility == KnowledgeVisibility.Public || item.UserId == actorId || manager)
                      orderby item.UpdatedAt descending, item.Id descending
                      select new AnnouncementModel
                      {
                          Id = item.Id, Content = item.Content, UserId = item.UserId,
                          Visibility = item.Visibility, CreatedAt = item.CreatedAt, UpdatedAt = item.UpdatedAt,
                          AuthorDisplayName = author.DisplayName, AuthorUsername = author.UserName ?? ""
                      }).ToListAsync(ct);
    }

    public async Task<AnnouncementModel?> FindAsync(int id, ClaimsPrincipal actor, CancellationToken ct = default)
    {
        if (!await Allowed(actor, ct) && !await access.HasAccessAsync(actor, AccessKeys.Dashboard, ct)) return null;
        await using var db = await factory.CreateDbContextAsync(ct);
        var actorId = DocumentAccess.ActorId(actor);
        var manager = await DocumentAccess.IsManagerAsync(db, actorId, ct);
        return await (from item in db.Announcements.AsNoTracking()
                      join author in db.Users.IgnoreQueryFilters().AsNoTracking() on item.UserId equals author.Id
                      where item.Id == id && !item.IsDeleted &&
                            (item.Visibility == KnowledgeVisibility.Public || item.UserId == actorId || manager)
                      select new AnnouncementModel
                      {
                          Id = item.Id, Content = item.Content, UserId = item.UserId,
                          Visibility = item.Visibility, CreatedAt = item.CreatedAt, UpdatedAt = item.UpdatedAt,
                          AuthorDisplayName = author.DisplayName, AuthorUsername = author.UserName ?? ""
                      }).FirstOrDefaultAsync(ct);
    }

    public async Task<KnowledgeSaveResult> SaveAsync(AnnouncementModel item, ClaimsPrincipal actor, CancellationToken ct = default)
    {
        if (!await Allowed(actor, ct)) return KnowledgeSaveResult.Fail("شما به اطلاعیه‌ها دسترسی ندارید.");
        var actorId = DocumentAccess.ActorId(actor);
        if (actorId == 0) return KnowledgeSaveResult.Fail("لطفاً دوباره وارد شوید.");
        var content = (item.Content ?? "").Trim();
        if (content.Length is < 1 or > 4000) return KnowledgeSaveResult.Fail("متن اطلاعیه باید بین ۱ تا ۴۰۰۰ نویسه باشد.");
        await using var db = await factory.CreateDbContextAsync(ct);
        var manager = await DocumentAccess.IsManagerAsync(db, actorId, ct);
        var authorId = item.UserId == 0 ? actorId : item.UserId;
        if (!manager && authorId != actorId) return KnowledgeSaveResult.Fail("فقط مدیر می‌تواند نویسنده دیگری انتخاب کند.");
        if (!await db.Users.AnyAsync(x => x.Id == authorId, ct)) return KnowledgeSaveResult.Fail("نویسنده معتبر نیست.");
        Announcement entity;
        if (item.Id == 0)
        {
            entity = new Announcement { CreatedAt = DateTimeOffset.UtcNow };
            db.Announcements.Add(entity);
        }
        else
        {
            entity = await db.Announcements.FirstOrDefaultAsync(x => x.Id == item.Id && !x.IsDeleted, ct)
                ?? throw new KeyNotFoundException("اطلاعیه پیدا نشد.");
            if (entity.UserId != actorId && !manager) return KnowledgeSaveResult.Fail("اجازه ویرایش ندارید.");
        }
        entity.Content = content;
        entity.UserId = authorId;
        entity.Visibility = item.Visibility;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return KnowledgeSaveResult.Success(entity.Id);
    }

    public async Task<KnowledgeSaveResult> DeleteAsync(int id, ClaimsPrincipal actor, CancellationToken ct = default)
    {
        if (!await Allowed(actor, ct)) return KnowledgeSaveResult.Fail("شما به اطلاعیه‌ها دسترسی ندارید.");
        var actorId = DocumentAccess.ActorId(actor);
        if (actorId == 0) return KnowledgeSaveResult.Fail("لطفاً دوباره وارد شوید.");
        await using var db = await factory.CreateDbContextAsync(ct);
        var item = await db.Announcements.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct);
        if (item is null) return KnowledgeSaveResult.Fail("اطلاعیه پیدا نشد.");
        if (item.UserId != actorId && !await DocumentAccess.IsManagerAsync(db, actorId, ct))
            return KnowledgeSaveResult.Fail("اجازه حذف ندارید.");
        item.IsDeleted = true;
        await db.SaveChangesAsync(ct);
        return KnowledgeSaveResult.Success(id);
    }
}
