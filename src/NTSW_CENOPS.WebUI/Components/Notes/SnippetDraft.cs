namespace NTSW_CENOPS.Components.Notes;

/// <summary>Transient editor input. Content is never persisted until the destination form is saved.</summary>
public sealed class SnippetDraft
{
    public string Kind { get; set; } = "";
    public string Content { get; set; } = "";
    public string Language { get; set; } = "plain";
    public string BlockKind { get; set; } = "code";
}
