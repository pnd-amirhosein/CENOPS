namespace NTSW_CENOPS.Infrastructure.Stories;

/// <summary>A permanent immutable snapshot of a release document, indexed by its source filename.</summary>
public sealed class ReleaseStory
{
    public int Id { get; set; }
    public string SourceFile { get; set; } = "";
    public string Title { get; set; } = "";
    public string Version { get; set; } = "";
    public string Markdown { get; set; } = "";
    public DateTimeOffset SourceCreatedAt { get; set; }
    public DateTimeOffset ImportedAt { get; set; }
}

/// <summary>One first-view receipt per user and release story.</summary>
public sealed class ReleaseStoryView
{
    public int StoryId { get; set; }
    public int UserId { get; set; }
    public DateTimeOffset ViewedAt { get; set; }
}
