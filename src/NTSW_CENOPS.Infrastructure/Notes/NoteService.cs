using System.Security.Claims;
using NTSW_CENOPS.Application.Access;
using NTSW_CENOPS.Application.Knowledge;
using NTSW_CENOPS.Application.Notes;
using NTSW_CENOPS.Application.Files;
using NTSW_CENOPS.Domain.Authorization;
using NTSW_CENOPS.Infrastructure.Identity;
using NTSW_CENOPS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace NTSW_CENOPS.Infrastructure.Notes;

public sealed class NoteService(IDbContextFactory<AppDbContext> factory, IAccessService access,
    IAppFileGateway files) : INoteService
{
    private static int ActorId(ClaimsPrincipal actor) =>
        int.TryParse(actor.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;

    private Task<bool> Allowed(ClaimsPrincipal actor, CancellationToken ct) =>
        access.HasAccessAsync(actor, AccessKeys.Notes, ct);

    private static Task<bool> IsManager(AppDbContext db, int id, CancellationToken ct) =>
        (from membership in db.UserRoles
         join role in db.Roles on membership.RoleId equals role.Id
         where membership.UserId == id && (role.Name == RoleNames.Owner || role.Name == RoleNames.Admin)
         select membership.RoleId).AnyAsync(ct);

    public async Task<bool> CanChooseAuthorAsync(ClaimsPrincipal actor, CancellationToken ct = default)
    {
        if (!await Allowed(actor, ct) || ActorId(actor) == 0) return false;
        await using var db = await factory.CreateDbContextAsync(ct);
        return await IsManager(db, ActorId(actor), ct);
    }

    public async Task<IReadOnlyList<KnowledgeAuthorModel>> AuthorsAsync(ClaimsPrincipal actor, CancellationToken ct = default)
    {
        if (!await Allowed(actor, ct)) return [];
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Users.AsNoTracking().OrderBy(x => x.DisplayName)
            .Select(x => new KnowledgeAuthorModel(x.Id, x.UserName ?? "", x.DisplayName)).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<NoteModel>> ListAsync(ClaimsPrincipal actor, CancellationToken ct = default)
    {
        if (!await Allowed(actor, ct)) return [];
        await using var db = await factory.CreateDbContextAsync(ct);
        var actorId = ActorId(actor);
        var privileged = await IsManager(db, actorId, ct);
        return await (from note in db.Notes.AsNoTracking()
                      where !note.IsDeleted && (note.Visibility == KnowledgeVisibility.Public || note.UserId == actorId || privileged)
                      join author in db.Users.IgnoreQueryFilters().AsNoTracking() on note.UserId equals author.Id
                      orderby note.UpdatedAt descending, note.Id descending
                      select new NoteModel
                      {
                          Id = note.Id, Title = note.Title, UserId = note.UserId, Visibility = note.Visibility,
                          Excerpt = note.Excerpt, UpdatedAt = note.UpdatedAt,
                          AuthorDisplayName = author.DisplayName,
                          AuthorUsername = author.UserName ?? ""
                      }).ToListAsync(ct);
    }

    public async Task<NoteModel?> FindAsync(int id, ClaimsPrincipal actor, CancellationToken ct = default)
    {
        if (!await Allowed(actor, ct) && !await access.HasAccessAsync(actor, AccessKeys.Dashboard, ct)) return null;
        await using var db = await factory.CreateDbContextAsync(ct);
        var note = await db.Notes.AsNoTracking().Include(x => x.Blocks)
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct);
        if (note is null) return null;
        if (note.Visibility == KnowledgeVisibility.Private && note.UserId != ActorId(actor) &&
            !await IsManager(db, ActorId(actor), ct)) return null;
        var author = await db.Users.IgnoreQueryFilters().AsNoTracking().Where(x => x.Id == note.UserId)
            .Select(x => new { x.DisplayName, x.UserName }).FirstOrDefaultAsync(ct);
        return new NoteModel
        {
            Id = note.Id, Title = note.Title, UserId = note.UserId, Visibility = note.Visibility,
            AuthorDisplayName = author?.DisplayName ?? "کاربر حذف‌شده",
            AuthorUsername = author?.UserName ?? "", Excerpt = note.Excerpt,
            UpdatedAt = note.UpdatedAt,
            Blocks = note.Blocks.OrderBy(x => x.Position).Select(x => new NoteBlockModel
            {
                Id = x.Id, Kind = x.Kind, Content = x.Content, Language = x.Language, RowGroup = x.RowGroup
            }).ToList()
        };
    }

    public async Task<bool> CanManageAsync(NoteModel note, ClaimsPrincipal actor, CancellationToken ct = default)
    {
        if (!await Allowed(actor, ct) || ActorId(actor) == 0 || note.Id <= 0) return false;
        await using var db = await factory.CreateDbContextAsync(ct);
        // Never trust a caller-provided UserId when deciding who may edit an existing note.
        var realAuthorId = await db.Notes.AsNoTracking()
            .Where(x => x.Id == note.Id && !x.IsDeleted)
            .Select(x => (int?)x.UserId).FirstOrDefaultAsync(ct);
        if (realAuthorId is null) return false;
        return realAuthorId == ActorId(actor) || await IsManager(db, ActorId(actor), ct);
    }

    public async Task<KnowledgeSaveResult> SaveAsync(NoteModel note, ClaimsPrincipal actor, CancellationToken ct = default)
    {
        if (!await Allowed(actor, ct)) return KnowledgeSaveResult.Fail("شما به یادداشت‌ها دسترسی ندارید.");
        var actorId = ActorId(actor);
        if (actorId == 0) return KnowledgeSaveResult.Fail("لطفاً دوباره وارد شوید.");
        var title = (note.Title ?? "").Trim();
        if (title.Length is < 1 or > 250) return KnowledgeSaveResult.Fail("عنوان یادداشت الزامی است و حداکثر ۲۵۰ نویسه دارد.");
        if (note.Blocks is null || note.Blocks.Count > 120)
            return KnowledgeSaveResult.Fail("هر یادداشت حداکثر ۱۲۰ بخش می‌تواند داشته باشد.");

        var blocks = note.Blocks.Where(x => !string.IsNullOrWhiteSpace(x.Content)).ToList();
        if (blocks.Any(x => !Enum.IsDefined(x.Kind)))
            return KnowledgeSaveResult.Fail("نوع یکی از بخش‌ها معتبر نیست.");
        if (blocks.Any(x => x.Content.Length > (x.Kind switch
        {
            NoteBlockKind.Heading => 250,
            NoteBlockKind.Code or NoteBlockKind.Table => 50000,
            NoteBlockKind.File => 8000,
            _ => 12000
        })) ||
            blocks.Sum(x => x.Content.Length) > 250000)
            return KnowledgeSaveResult.Fail("حجم محتوای یادداشت بیشتر از حد مجاز است.");
        var groupSpans = blocks.Select((x, i) => new { x.RowGroup, Index = i })
            .Where(x => x.RowGroup.HasValue).GroupBy(x => x.RowGroup!.Value);
        if (groupSpans.Any(g => g.Count() > 4 || g.Max(x => x.Index) - g.Min(x => x.Index) + 1 != g.Count()))
            return KnowledgeSaveResult.Fail("چیدمان بخش‌های کنار هم معتبر نیست.");
        if (blocks.Any(x => x.Kind == NoteBlockKind.File && !FileReferenceCodec.IsValid(x.Content)))
            return KnowledgeSaveResult.Fail("فهرست فایل‌های پیوست معتبر نیست.");
        if (blocks.Any(x => x.Kind == NoteBlockKind.Table && !NoteTableData.IsValid(x.Content)))
            return KnowledgeSaveResult.Fail("ساختار جدول یادداشت معتبر نیست.");
        if (blocks.Any(x => (x.Language ?? "").Length > 40))
            return KnowledgeSaveResult.Fail("نام زبان کد بیش از حد طولانی است.");

        await using var db = await factory.CreateDbContextAsync(ct);
        var manager = await IsManager(db, actorId, ct);
        var authorId = note.UserId == 0 ? actorId : note.UserId;
        if (!manager && authorId != actorId)
            return KnowledgeSaveResult.Fail("فقط مدیر می‌تواند نویسنده دیگری انتخاب کند.");
        if (!await db.Users.AnyAsync(x => x.Id == authorId, ct))
            return KnowledgeSaveResult.Fail("نویسنده انتخاب‌شده معتبر نیست.");

        // A client must not publish someone else's private file by placing its UUID
        // inside a public Note. Existing links are allowed for edits/migrations.
        var requestedFileIds = blocks.Where(x => x.Kind == NoteBlockKind.File)
            .SelectMany(x => FileReferenceCodec.Parse(x.Content)).Distinct().ToArray();
        var previousFileIds = new HashSet<Guid>();
        if (note.Id > 0)
        {
            var oldFileBlocks = await db.NoteBlocks.AsNoTracking()
                .Where(x => x.NoteId == note.Id && x.Kind == NoteBlockKind.File)
                .Select(x => x.Content).ToListAsync(ct);
            previousFileIds.UnionWith(oldFileBlocks.SelectMany(FileReferenceCodec.Parse));
        }
        foreach (var fileId in requestedFileIds.Where(x => !previousFileIds.Contains(x)))
        {
            var file = await files.FindAsync(fileId, ct);
            if (file?.OwnerKey != actorId.ToString(System.Globalization.CultureInfo.InvariantCulture))
                return KnowledgeSaveResult.Fail("این فایل متعلق به شما نیست یا در دسترس نیست.");
        }

        Note entity;
        if (note.Id == 0)
        {
            entity = new Note { CreatedAt = DateTimeOffset.UtcNow };
            db.Notes.Add(entity);
        }
        else
        {
            entity = await db.Notes.Include(x => x.Blocks).FirstOrDefaultAsync(x => x.Id == note.Id && !x.IsDeleted, ct)
                ?? throw new KeyNotFoundException("یادداشت پیدا نشد.");
            if (entity.UserId != actorId && !manager)
                return KnowledgeSaveResult.Fail("اجازه ویرایش این یادداشت را ندارید.");
            db.NoteBlocks.RemoveRange(entity.Blocks);
            entity.Blocks.Clear();
        }
        entity.Title = title;
        entity.UserId = authorId;
        entity.Visibility = note.Visibility;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        entity.Excerpt = (blocks.FirstOrDefault(x => x.Kind == NoteBlockKind.Paragraph)?.Content ??
            blocks.FirstOrDefault(x => x.Kind is NoteBlockKind.Heading or NoteBlockKind.Code)?.Content ?? string.Empty).Trim();
        if (entity.Excerpt.Length > 300) entity.Excerpt = entity.Excerpt[..300];
        entity.Blocks = blocks.Select((x, i) => new NoteBlock
        {
            Position = i, RowGroup = x.RowGroup, Kind = x.Kind,
            Content = x.Content.Trim(), Language = x.Kind == NoteBlockKind.Code ? (x.Language ?? "").Trim() : ""
        }).ToList();
        await db.SaveChangesAsync(ct);
        return KnowledgeSaveResult.Success(entity.Id);
    }

    public async Task<KnowledgeSaveResult> DeleteAsync(int id, ClaimsPrincipal actor, CancellationToken ct = default)
    {
        if (!await Allowed(actor, ct)) return KnowledgeSaveResult.Fail("شما به یادداشت‌ها دسترسی ندارید.");
        var actorId = ActorId(actor);
        if (actorId == 0) return KnowledgeSaveResult.Fail("لطفاً دوباره وارد شوید.");
        await using var db = await factory.CreateDbContextAsync(ct);
        var entity = await db.Notes.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct);
        if (entity is null) return KnowledgeSaveResult.Fail("یادداشت پیدا نشد.");
        if (entity.UserId != actorId && !await IsManager(db, actorId, ct))
            return KnowledgeSaveResult.Fail("اجازه حذف این یادداشت را ندارید.");
        entity.IsDeleted = true;
        await db.SaveChangesAsync(ct);
        return KnowledgeSaveResult.Success(id);
    }
}
