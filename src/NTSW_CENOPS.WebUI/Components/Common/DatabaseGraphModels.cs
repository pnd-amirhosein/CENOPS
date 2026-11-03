namespace NTSW_CENOPS.Components.Common;

public sealed record DatabaseGraphNode(int Id, string Schema, string Name, int Level,
    bool PkLoaded = false, bool FkLoaded = false);
public sealed record DatabaseGraphEdge(int Id, int ChildId, int ParentId, string Name, string Columns);

// The renderer's undo/redo journal can restore the visible SQL metadata without querying again.
public sealed record DatabaseGraphSnapshot(
    IReadOnlyList<DatabaseGraphNode> Nodes, IReadOnlyList<DatabaseGraphEdge> Edges);

/// <summary>Immutable selection taken when a SQL export command is clicked.</summary>
public sealed record DatabaseQueryExportRequest(string Mode, IReadOnlyList<int> TableIds);

/// <summary>Server-validated metadata plan. Includes unselected FK bridge tables.</summary>
public sealed record DatabaseJoinPlan(IReadOnlyList<int> TableIds, IReadOnlyList<int> RelationshipIds);
