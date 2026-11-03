namespace NTSW_CENOPS.Components.SideBar;

public sealed class SidebarItem
{
    public string Title { get; set; } = string.Empty;
    public string? IconSvg { get; set; }
    public string? Href { get; set; }
    public string? AllowedRoles { get; set; }
    public string? RequiredAccess { get; set; }
    public string? Accent { get; set; }

    public bool IsParent => Children?.Any() == true;
    public List<SidebarItem>? Children { get; set; }
    public bool IsExpanded { get; set; }
}
