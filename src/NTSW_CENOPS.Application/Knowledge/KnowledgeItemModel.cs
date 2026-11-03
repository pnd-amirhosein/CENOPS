namespace NTSW_CENOPS.Application.Knowledge;

public sealed class KnowledgeItemModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    // A short display-name alias, synchronized with Title by the editor.
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public int UserId { get; set; }
    public KnowledgeVisibility Visibility { get; set; } = KnowledgeVisibility.Public;
    public string AuthorDisplayName { get; set; } = string.Empty;
    public string AuthorUsername { get; set; } = string.Empty;
}

public sealed record KnowledgeAuthorModel(int Id, string Username, string DisplayName);
public sealed record KnowledgeSaveResult(bool Succeeded, string Message, int Id = 0)
{
    public static KnowledgeSaveResult Success(int id) => new(true, string.Empty, id);
    public static KnowledgeSaveResult Fail(string message) => new(false, message);
}
