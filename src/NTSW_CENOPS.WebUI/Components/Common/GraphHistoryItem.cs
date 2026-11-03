namespace NTSW_CENOPS.Components.Common;

/// <summary>An immutable, UI-facing step in the renderer's bounded undo timeline.</summary>
public sealed record GraphHistoryItem(int Index, string Label);
