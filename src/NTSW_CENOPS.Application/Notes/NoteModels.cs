using NTSW_CENOPS.Application.Knowledge;

namespace NTSW_CENOPS.Application.Notes;

public enum NoteBlockKind : byte { Heading = 1, Paragraph = 2, Code = 3, Table = 4, File = 5 }

public sealed class NoteBlockModel
{
    public int Id { get; set; }
    public NoteBlockKind Kind { get; set; }
    public string Content { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    // Null means this block owns a full row; shared IDs form a side-by-side row.
    public Guid? RowGroup { get; set; }
    // UI-only identity so inserting/reordering blocks preserves input focus and component state.
    public Guid Key { get; set; } = Guid.NewGuid();
}

public sealed class NoteModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public int UserId { get; set; }
    public KnowledgeVisibility Visibility { get; set; } = KnowledgeVisibility.Public;
    public string AuthorDisplayName { get; set; } = string.Empty;
    public string AuthorUsername { get; set; } = string.Empty;
    public string Excerpt { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; set; }
    public List<NoteBlockModel> Blocks { get; set; } = [];
}
