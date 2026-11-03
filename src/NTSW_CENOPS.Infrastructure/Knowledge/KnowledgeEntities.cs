using NTSW_CENOPS.Application.Knowledge;

namespace NTSW_CENOPS.Infrastructure.Knowledge;

public abstract class KnowledgeEntryBase
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public int UserId { get; set; }
    public KnowledgeVisibility Visibility { get; set; } = KnowledgeVisibility.Public;
    public bool IsDeleted { get; set; }
}

public sealed class Curl : KnowledgeEntryBase { }
public sealed class SqlQuery : KnowledgeEntryBase { }
