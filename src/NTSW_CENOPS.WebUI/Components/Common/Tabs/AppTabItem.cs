namespace NTSW_CENOPS.Components.Common.Tabs;

public sealed class AppTabItem
{
    public required string Key { get; init; }
    public required string Title { get; init; }
    // Raw trusted application SVG; omit for text-only tabs.
    public string IconSvg { get; init; } = string.Empty;
}
