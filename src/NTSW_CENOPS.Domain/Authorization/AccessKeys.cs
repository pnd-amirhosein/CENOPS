namespace NTSW_CENOPS.Domain.Authorization;

/// <summary>Stable permission codes. UI text and database display names may change independently.</summary>
public static class AccessKeys
{
    public const string SourceIntelligence = "tools.source-intelligence";
    public const string DatabaseInspector = "tools.database-inspector";
    public const string Events = "team.events.view";
    public const string Dashboard = "pages.dashboard";
    public const string Contacts = "pages.contacts";
    public const string HealthApis = "pages.health-apis";
    public const string Curls = "wiki.curls.view";
    public const string SqlQueries = "wiki.queries.view";
    public const string Notes = "wiki.notes.view";
    public const string Todos = "wiki.todos.view";
    public const string Announcements = "wiki.announcements.view";
    public const string Chat = "chat.use";
    public const string Weather = "pages.weather";
    public const string Settings = "settings.view";
    public const string ViewJobs = "settings.jobs.view";
    public const string ManageJobs = "settings.jobs.manage";
    public const string ExecuteJobs = "settings.jobs.execute";
    public const string ManageAccess = "settings.access.manage";
    public const string ViewReports = "reports.view";
    public const string ManageReports = "reports.manage";
}
