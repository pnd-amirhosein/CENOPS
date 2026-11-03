namespace NTSW_CENOPS.Components.Common;

/// <summary>Presentation-only graph contracts; reusable independently of Source Intelligence indexing.</summary>
public sealed record SourceGraphNode(string Id, string Title, string Subtitle, string Kind, int Level,
    string Project = "", string File = "", int Line = 0, string Namespace = "", string Code = "",
    string? ExpandSymbolId = null, int ReferenceCount = 0, string Signature = "", bool IsCycle = false, string Relationship = "");

public sealed record SourceGraphEdge(string From, string To, string Kind = "Reference");
