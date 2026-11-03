namespace NTSW_CENOPS.Infrastructure.Dashboards;

/// <summary>Exactly one shared dashboard area; the row has a stable key of 1.</summary>
public sealed class GlobalDashboardConfiguration
{
    public int Id { get; set; } = 1;
    public string Title { get; set; } = "داشبورد عمومی";
    public string LayoutJson { get; set; } = "[]";
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>Independent dashboards owned by a single user; at most one default per user.</summary>
public sealed class UserDashboardConfiguration
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Name { get; set; } = "داشبورد من";
    public string LayoutJson { get; set; } = "[]";
    public bool IsDefault { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>A viewer-specific default for the collection of publicly browseable dashboards.</summary>
public sealed class DashboardViewerPreference
{
    public int UserId { get; set; }
    public int DashboardId { get; set; }
}
