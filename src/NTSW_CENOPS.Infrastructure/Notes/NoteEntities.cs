using NTSW_CENOPS.Application.Notes;
using NTSW_CENOPS.Application.Knowledge;

namespace NTSW_CENOPS.Infrastructure.Notes;

public sealed class Note
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public int UserId { get; set; }
    public KnowledgeVisibility Visibility { get; set; } = KnowledgeVisibility.Public;
    public string Excerpt { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public List<NoteBlock> Blocks { get; set; } = [];
}

public sealed class NoteBlock
{
    public int Id { get; set; }
    public int NoteId { get; set; }
    public int Position { get; set; }
    public Guid? RowGroup { get; set; }
    public NoteBlockKind Kind { get; set; }
    public string Content { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
}
