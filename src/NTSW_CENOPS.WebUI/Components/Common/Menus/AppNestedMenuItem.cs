namespace NTSW_CENOPS.Components.Common.Menus;

/// <summary>A command or expandable group in the reusable context-menu drawer.</summary>
public sealed record AppNestedMenuItem(
    string Value,
    string Label,
    string? Hint = null,
    IReadOnlyList<AppNestedMenuItem>? Children = null,
    bool Disabled = false,
    int? Count = null);
