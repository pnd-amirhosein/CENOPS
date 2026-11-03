using System.Security.Claims;
using NTSW_CENOPS.Application.Access;
using NTSW_CENOPS.Application.Knowledge;
using NTSW_CENOPS.Application.Tasks;
using NTSW_CENOPS.Domain.Authorization;
using NTSW_CENOPS.Infrastructure.Knowledge;
using NTSW_CENOPS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace NTSW_CENOPS.Infrastructure.Tasks;

public sealed class TodoService(IDbContextFactory<AppDbContext> factory, IAccessService access) : ITodoService
{
    private Task<bool> Allowed(ClaimsPrincipal actor, CancellationToken ct) =>
        access.HasAccessAsync(actor, AccessKeys.Todos, ct);

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

    public async Task<IReadOnlyList<TodoModel>> ListAsync(ClaimsPrincipal actor, CancellationToken ct = default)
    {
        if (!await Allowed(actor, ct)) return [];
        await using var db = await factory.CreateDbContextAsync(ct);
        var actorId = DocumentAccess.ActorId(actor);
        var manager = await DocumentAccess.IsManagerAsync(db, actorId, ct);
        return await (from todo in db.Todos.AsNoTracking()
                      join author in db.Users.IgnoreQueryFilters().AsNoTracking() on todo.UserId equals author.Id
                      where !todo.IsDeleted && (todo.Visibility == KnowledgeVisibility.Public || todo.UserId == actorId || manager)
                      orderby todo.UpdatedAt descending, todo.Id descending
                      select new TodoModel
                      {
                          Id = todo.Id, Title = todo.Title, UserId = todo.UserId,
                          Visibility = todo.Visibility, Excerpt = todo.Excerpt, UpdatedAt = todo.UpdatedAt,
                          AuthorDisplayName = author.DisplayName, AuthorUsername = author.UserName ?? ""
                      }).ToListAsync(ct);
    }

    public async Task<TodoModel?> FindAsync(int id, ClaimsPrincipal actor, CancellationToken ct = default)
    {
        if (!await Allowed(actor, ct) && !await access.HasAccessAsync(actor, AccessKeys.Dashboard, ct)) return null;
        await using var db = await factory.CreateDbContextAsync(ct);
        var actorId = DocumentAccess.ActorId(actor);
        var manager = await DocumentAccess.IsManagerAsync(db, actorId, ct);
        var todo = await db.Todos.AsNoTracking().Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted &&
                (x.Visibility == KnowledgeVisibility.Public || x.UserId == actorId || manager), ct);
        if (todo is null) return null;
        var author = await db.Users.IgnoreQueryFilters().AsNoTracking().Where(x => x.Id == todo.UserId)
            .Select(x => new { x.DisplayName, x.UserName }).FirstOrDefaultAsync(ct);
        return new TodoModel
        {
            Id = todo.Id, Title = todo.Title, UserId = todo.UserId,
            Visibility = todo.Visibility, Excerpt = todo.Excerpt, UpdatedAt = todo.UpdatedAt,
            AuthorDisplayName = author?.DisplayName ?? "کاربر حذف‌شده",
            AuthorUsername = author?.UserName ?? "",
            Items = todo.Items.OrderBy(x => x.Position).Select(x => new TodoItemModel
            {
                Id = x.Id, Text = x.Text, IsCompleted = x.IsCompleted
            }).ToList()
        };
    }

    public async Task<bool> CanManageAsync(TodoModel item, ClaimsPrincipal actor, CancellationToken ct = default)
    {
        if (!await Allowed(actor, ct) || DocumentAccess.ActorId(actor) == 0 || item.Id <= 0) return false;
        await using var db = await factory.CreateDbContextAsync(ct);
        var author = await db.Todos.AsNoTracking().Where(x => x.Id == item.Id && !x.IsDeleted)
            .Select(x => (int?)x.UserId).FirstOrDefaultAsync(ct);
        return author.HasValue && (author == DocumentAccess.ActorId(actor) ||
            await DocumentAccess.IsManagerAsync(db, DocumentAccess.ActorId(actor), ct));
    }

    public async Task<KnowledgeSaveResult> SaveAsync(TodoModel item, ClaimsPrincipal actor, CancellationToken ct = default)
    {
        if (!await Allowed(actor, ct)) return KnowledgeSaveResult.Fail("شما به کارها دسترسی ندارید.");
        var actorId = DocumentAccess.ActorId(actor);
        if (actorId == 0) return KnowledgeSaveResult.Fail("لطفاً دوباره وارد شوید.");
        var title = (item.Title ?? "").Trim();
        if (title.Length is < 1 or > 250) return KnowledgeSaveResult.Fail("عنوان باید بین ۱ تا ۲۵۰ نویسه باشد.");
        if (item.Items is null || item.Items.Count > 120) return KnowledgeSaveResult.Fail("حداکثر ۱۲۰ مورد مجاز است.");
        if (item.Items.Any(x => (x.Text ?? "").Length > 12000) ||
            item.Items.Sum(x => (x.Text ?? "").Length) > 250000)
            return KnowledgeSaveResult.Fail("طول متن موارد از حد مجاز بیشتر است.");
        var items = item.Items.Where(x => !string.IsNullOrWhiteSpace(x.Text)).ToList();
        await using var db = await factory.CreateDbContextAsync(ct);
        var manager = await DocumentAccess.IsManagerAsync(db, actorId, ct);
        var authorId = item.UserId == 0 ? actorId : item.UserId;
        if (!manager && authorId != actorId) return KnowledgeSaveResult.Fail("فقط مدیر می‌تواند نویسنده دیگری انتخاب کند.");
        if (!await db.Users.AnyAsync(x => x.Id == authorId, ct)) return KnowledgeSaveResult.Fail("نویسنده معتبر نیست.");
        Todo entity;
        if (item.Id == 0)
        {
            entity = new Todo { CreatedAt = DateTimeOffset.UtcNow };
            db.Todos.Add(entity);
        }
        else
        {
            entity = await db.Todos.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == item.Id && !x.IsDeleted, ct)
                ?? throw new KeyNotFoundException("فهرست کار پیدا نشد.");
            if (entity.UserId != actorId && !manager) return KnowledgeSaveResult.Fail("اجازه ویرایش ندارید.");
            db.TodoItems.RemoveRange(entity.Items);
            entity.Items.Clear();
        }
        entity.Title = title;
        entity.UserId = authorId;
        entity.Visibility = item.Visibility;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        entity.Excerpt = (items.FirstOrDefault()?.Text ?? "").Trim();
        if (entity.Excerpt.Length > 300) entity.Excerpt = entity.Excerpt[..300];
        entity.Items = items.Select((x, position) => new TodoItem
        {
            Position = position, Text = x.Text.Trim(), IsCompleted = x.IsCompleted
        }).ToList();
        await db.SaveChangesAsync(ct);
        return KnowledgeSaveResult.Success(entity.Id);
    }

    public async Task<KnowledgeSaveResult> DeleteAsync(int id, ClaimsPrincipal actor, CancellationToken ct = default)
    {
        if (!await Allowed(actor, ct)) return KnowledgeSaveResult.Fail("شما به کارها دسترسی ندارید.");
        var actorId = DocumentAccess.ActorId(actor);
        if (actorId == 0) return KnowledgeSaveResult.Fail("لطفاً دوباره وارد شوید.");
        await using var db = await factory.CreateDbContextAsync(ct);
        var todo = await db.Todos.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct);
        if (todo is null) return KnowledgeSaveResult.Fail("فهرست کار پیدا نشد.");
        if (todo.UserId != actorId && !await DocumentAccess.IsManagerAsync(db, actorId, ct))
            return KnowledgeSaveResult.Fail("اجازه حذف ندارید.");
        todo.IsDeleted = true;
        await db.SaveChangesAsync(ct);
        return KnowledgeSaveResult.Success(id);
    }
}
