using System.Security.Claims;
using NTSW_CENOPS.Application.Knowledge;

namespace NTSW_CENOPS.Application.Tasks;

public sealed class TodoItemModel
{
    public int Id { get; set; }
    public string Text { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
    public Guid Key { get; set; } = Guid.NewGuid(); // UI-only identity for stable drag behavior
}

public sealed class TodoModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public int UserId { get; set; }
    public string AuthorDisplayName { get; set; } = string.Empty;
    public string AuthorUsername { get; set; } = string.Empty;
    public string Excerpt { get; set; } = string.Empty;
    public KnowledgeVisibility Visibility { get; set; } = KnowledgeVisibility.Public;
    public DateTimeOffset UpdatedAt { get; set; }
    public List<TodoItemModel> Items { get; set; } = [];
}

public interface ITodoService
{
    Task<IReadOnlyList<TodoModel>> ListAsync(ClaimsPrincipal actor, CancellationToken ct = default);
    Task<TodoModel?> FindAsync(int id, ClaimsPrincipal actor, CancellationToken ct = default);
    Task<IReadOnlyList<KnowledgeAuthorModel>> AuthorsAsync(ClaimsPrincipal actor, CancellationToken ct = default);
    Task<bool> CanChooseAuthorAsync(ClaimsPrincipal actor, CancellationToken ct = default);
    Task<bool> CanManageAsync(TodoModel item, ClaimsPrincipal actor, CancellationToken ct = default);
    Task<KnowledgeSaveResult> SaveAsync(TodoModel item, ClaimsPrincipal actor, CancellationToken ct = default);
    Task<KnowledgeSaveResult> DeleteAsync(int id, ClaimsPrincipal actor, CancellationToken ct = default);
}
