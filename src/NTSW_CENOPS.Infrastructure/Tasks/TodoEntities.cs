using NTSW_CENOPS.Application.Knowledge;

namespace NTSW_CENOPS.Infrastructure.Tasks;

public sealed class Todo
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public int UserId { get; set; }
    public KnowledgeVisibility Visibility { get; set; } = KnowledgeVisibility.Public;
    public string Excerpt { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public List<TodoItem> Items { get; set; } = [];
}

public sealed class TodoItem
{
    public int Id { get; set; }
    public int TodoId { get; set; }
    public int Position { get; set; }
    public string Text { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
}
