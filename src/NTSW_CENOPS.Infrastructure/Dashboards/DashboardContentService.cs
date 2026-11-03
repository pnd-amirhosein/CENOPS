using System.Security.Claims;
using NTSW_CENOPS.Application.Access;
using NTSW_CENOPS.Application.Dashboards;
using NTSW_CENOPS.Application.Events;
using NTSW_CENOPS.Application.Knowledge;
using NTSW_CENOPS.Domain.Authorization;
using NTSW_CENOPS.Infrastructure.Knowledge;
using NTSW_CENOPS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace NTSW_CENOPS.Infrastructure.Dashboards;

/// <summary>
/// Provides permission-filtered dashboard cards. Search runs on the database, including
/// note blocks, todo items and SQL/cURL bodies; only a short preview is returned.
/// The same resolver is used when saving layout JSON and rendering saved references.
/// </summary>
public sealed class DashboardContentService(IDbContextFactory<AppDbContext> factory, IAccessService access)
    : IDashboardContentService
{
    public const int MaxItems = 60;
    private const int SearchLimit = 180;
    private static readonly Dictionary<string, string> Permissions = new(StringComparer.Ordinal)
    {
        ["note"] = AccessKeys.Notes, ["announcement"] = AccessKeys.Announcements,
        ["todo"] = AccessKeys.Todos, ["sql"] = AccessKeys.SqlQueries,
        ["curl"] = AccessKeys.Curls, ["report"] = AccessKeys.ViewReports,
        ["event"] = AccessKeys.Events, ["events-day"] = AccessKeys.Events,
        ["events-week"] = AccessKeys.Events, ["events-month"] = AccessKeys.Events,
        ["smart"] = AccessKeys.Dashboard
    };

    public async Task<IReadOnlyList<DashboardContentItem>> SearchAsync(
        string? query, string? category, bool publicOnly, ClaimsPrincipal actor, CancellationToken ct = default)
    {
        var term = (query ?? "").Trim();
        if (term.Length > 150) throw new ArgumentException("عبارت جستجو بیش از حد طولانی است.");
        if (category is not null && category is not ("all" or "events") && !Permissions.ContainsKey(category))
            throw new ArgumentException("دسته‌بندی معتبر نیست.");
        return await FetchAsync(term, category, publicOnly, null, actor, ct);
    }

    public async Task<IReadOnlyList<DashboardContentItem>> ResolveAsync(
        IReadOnlyList<DashboardContentReference> references, bool publicOnly,
        ClaimsPrincipal actor, CancellationToken ct = default)
    {
        if (references.Count == 0) return [];
        if (references.Count > MaxItems || references.Any(x => x.Id <= 0 || !Permissions.ContainsKey(x.Type)) ||
            references.Select(x => (x.Type, x.Id)).Distinct().Count() != references.Count)
            throw new ArgumentException("چیدمان شامل محتوای نامعتبر یا تکراری است.");
        var wanted = references.GroupBy(x => x.Type).ToDictionary(
            g => g.Key, g => g.Select(x => x.Id).ToHashSet(), StringComparer.Ordinal);
        var found = await FetchAsync("", null, publicOnly, wanted, actor, ct);
        var byKey = found.ToDictionary(x => (x.Type, x.Id));
        // Keep every saved slot. Non-authorized cards must never reveal title,
        // author, descriptions, task text, code, or chart data to other viewers.
        // Unknown/deleted references use the same redacted presentation to avoid
        // disclosing whether an inaccessible document exists.
        return references.Select(x => byKey.TryGetValue((x.Type, x.Id), out var visible)
            ? visible
            : new DashboardContentItem(x.Type, x.Id, "محتوای غیرقابل دسترس", "", "", false, "", IsRestricted: true))
            .ToList();
    }

    private async Task<IReadOnlyList<DashboardContentItem>> FetchAsync(
        string term, string? category, bool publicOnly, Dictionary<string, HashSet<long>>? wanted,
        ClaimsPrincipal actor, CancellationToken ct)
    {
        var actorId = DocumentAccess.ActorId(actor);
        if (actorId == 0 || !await access.HasAccessAsync(actor, AccessKeys.Dashboard, ct))
            throw new UnauthorizedAccessException("مجوز مشاهده داشبورد را ندارید.");

        // Check each source section, rather than treating dashboard access as permission to read every wiki.
        var allowed = new HashSet<string>(StringComparer.Ordinal);
        foreach (var pair in Permissions)
            if ((category is null or "all" || category == pair.Key ||
                 (category == "events" && (pair.Key is "event" or "events-day" or "events-week" or "events-month"))) &&
                (wanted is null || wanted.ContainsKey(pair.Key)) &&
                // Picking NEW content still requires access to its module. Resolving
                // an EXISTING dashboard reference is permitted under Dashboard access:
                // only explicitly PUBLIC content (or private content belonging to
                // this viewer / a manager) passes the database visibility predicates.
                // Otherwise another author's public card would appear locked merely
                // because the viewer cannot open that module's management page.
                (wanted is not null || await access.HasAccessAsync(actor, pair.Value, ct)))
                allowed.Add(pair.Key);
        if (allowed.Count == 0) return [];

        await using var db = await factory.CreateDbContextAsync(ct);
        var manager = await DocumentAccess.IsManagerAsync(db, actorId, ct);
        var results = new List<DashboardContentItem>();
        bool Include(string type) => allowed.Contains(type);
        int[] Ids(string type) => wanted is null ? [] : wanted[type].Where(x => x is > 0 and <= int.MaxValue).Select(x => (int)x).ToArray();
        long[] LongIds(string type) => wanted is null ? [] : wanted[type].ToArray();
        var filterById = wanted is not null;
        var max = wanted is not null ? MaxItems : (category is null or "all") ? 30 : SearchLimit;

        if (Include("note"))
        {
            var ids = Ids("note");
            var rows = await (from n in db.Notes.AsNoTracking()
                join u in db.Users.IgnoreQueryFilters().AsNoTracking() on n.UserId equals u.Id
                where !n.IsDeleted && (!filterById || ids.Contains(n.Id)) &&
                      (n.Visibility == KnowledgeVisibility.Public || (!publicOnly && (manager || n.UserId == actorId))) &&
                      (term == "" || n.Title.Contains(term) || n.Excerpt.Contains(term) ||
                       n.Blocks.Any(b => b.Content.Contains(term)) || u.DisplayName.Contains(term) || (u.UserName ?? "").Contains(term))
                orderby n.UpdatedAt descending, n.Id descending
                select new { n.Id, n.Title, n.Excerpt, n.Visibility, Author = u.DisplayName })
                .Take(max).ToListAsync(ct);
            // Resolve the actual note rows for dashboard cards, not just the first paragraph.
            // Only IDs already authorized by the visibility query above are loaded; row groups
            // are kept so inline blocks survive both dashboard rendering and preview.
            var noteBlocks = new Dictionary<int, IReadOnlyList<NTSW_CENOPS.Application.Notes.NoteBlockModel>>();
            if (filterById && rows.Count > 0)
            {
                var visibleIds = rows.Select(n => n.Id).ToArray();
                var blocks = await db.NoteBlocks.AsNoTracking()
                    .Where(b => visibleIds.Contains(b.NoteId))
                    .OrderBy(b => b.NoteId).ThenBy(b => b.Position)
                    .Select(b => new { b.NoteId, b.Id, b.Kind, b.Content, b.Language, b.RowGroup })
                    .ToListAsync(ct);
                noteBlocks = blocks.GroupBy(b => b.NoteId).ToDictionary(
                    group => group.Key,
                    group => (IReadOnlyList<NTSW_CENOPS.Application.Notes.NoteBlockModel>)group
                        .Take(24) // A dashboard is an overview; full content remains in the preview.
                        .Select(b => new NTSW_CENOPS.Application.Notes.NoteBlockModel
                        { Id = b.Id, Kind = b.Kind, Content = b.Content, Language = b.Language, RowGroup = b.RowGroup })
                        .ToArray());
            }
            results.AddRange(rows.Select(n => new DashboardContentItem("note", n.Id, n.Title,
                Preview(n.Excerpt), n.Author, n.Visibility == KnowledgeVisibility.Public, $"/wiki/notes/edit/{n.Id}",
                NoteBlocks: noteBlocks.GetValueOrDefault(n.Id))));
        }
        if (Include("announcement"))
        {
            var ids = Ids("announcement");
            var rows = await (from n in db.Announcements.AsNoTracking()
                join u in db.Users.IgnoreQueryFilters().AsNoTracking() on n.UserId equals u.Id
                where !n.IsDeleted && (!filterById || ids.Contains(n.Id)) &&
                      (n.Visibility == KnowledgeVisibility.Public || (!publicOnly && (manager || n.UserId == actorId))) &&
                      (term == "" || n.Content.Contains(term) || u.DisplayName.Contains(term) || (u.UserName ?? "").Contains(term))
                orderby n.UpdatedAt descending, n.Id descending
                select new { n.Id, n.Content, n.Visibility, Author = u.DisplayName })
                .Take(max).ToListAsync(ct);
            results.AddRange(rows.Select(n => new DashboardContentItem("announcement", n.Id,
                n.Content, "", n.Author,
                n.Visibility == KnowledgeVisibility.Public, "/wiki/announcements")));
        }
        if (Include("todo"))
        {
            var ids = Ids("todo");
            var rows = await (from n in db.Todos.AsNoTracking()
                join u in db.Users.IgnoreQueryFilters().AsNoTracking() on n.UserId equals u.Id
                where !n.IsDeleted && (!filterById || ids.Contains(n.Id)) &&
                      (n.Visibility == KnowledgeVisibility.Public || (!publicOnly && (manager || n.UserId == actorId))) &&
                      (term == "" || n.Title.Contains(term) || n.Excerpt.Contains(term) ||
                       n.Items.Any(i => i.Text.Contains(term)) || u.DisplayName.Contains(term) || (u.UserName ?? "").Contains(term))
                orderby n.UpdatedAt descending, n.Id descending
                select new { n.Id, n.Title, n.Excerpt, n.Visibility, Author = u.DisplayName })
                .Take(max).ToListAsync(ct);
            // Fetch short task previews in one batch, not one database call per card.
            // Keep the full todo text exclusively in the permission-checked detail view.
            var todoIds = rows.Select(row => row.Id).ToArray();
            var taskRows = await db.TodoItems.AsNoTracking()
                .Where(item => todoIds.Contains(item.TodoId))
                .OrderBy(item => item.Position).ThenBy(item => item.Id)
                .Select(item => new { item.TodoId, Text = item.Text.Substring(0, 180), item.IsCompleted })
                .ToListAsync(ct);
            var taskPreviewByTodo = taskRows.GroupBy(item => item.TodoId).ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<DashboardTodoPreviewItem>)group.Take(3)
                    .Select(item => new DashboardTodoPreviewItem(item.Text, item.IsCompleted)).ToArray());
            results.AddRange(rows.Select(n => new DashboardContentItem("todo", n.Id, n.Title,
                Preview(n.Excerpt), n.Author, n.Visibility == KnowledgeVisibility.Public, $"/wiki/todos/edit/{n.Id}",
                TodoItems: taskPreviewByTodo.GetValueOrDefault(n.Id) ?? [])));
        }
        if (Include("sql"))
        {
            var ids = Ids("sql");
            var rows = await (from n in db.Queries.AsNoTracking()
                join u in db.Users.IgnoreQueryFilters().AsNoTracking() on n.UserId equals u.Id
                where !n.IsDeleted && (!filterById || ids.Contains(n.Id)) &&
                      (n.Visibility == KnowledgeVisibility.Public || (!publicOnly && (manager || n.UserId == actorId))) &&
                      (term == "" || n.Title.Contains(term) || n.Description.Contains(term) ||
                       n.Value.Contains(term) || u.DisplayName.Contains(term) || (u.UserName ?? "").Contains(term))
                orderby n.Id descending
                select new { n.Id, n.Title, n.Description, CodePreview = n.Value.Substring(0, 220), n.Visibility, Author = u.DisplayName })
                .Take(max).ToListAsync(ct);
            results.AddRange(rows.Select(n => new DashboardContentItem("sql", n.Id, n.Title,
                Preview(n.Description), n.Author,
                n.Visibility == KnowledgeVisibility.Public, $"/wiki/queries/edit/{n.Id}",
                CodePreview: n.CodePreview)));
        }
        if (Include("curl"))
        {
            var ids = Ids("curl");
            var rows = await (from n in db.Curls.AsNoTracking()
                join u in db.Users.IgnoreQueryFilters().AsNoTracking() on n.UserId equals u.Id
                where !n.IsDeleted && (!filterById || ids.Contains(n.Id)) &&
                      (n.Visibility == KnowledgeVisibility.Public || (!publicOnly && (manager || n.UserId == actorId))) &&
                      (term == "" || n.Title.Contains(term) || n.Description.Contains(term) ||
                       n.Value.Contains(term) || u.DisplayName.Contains(term) || (u.UserName ?? "").Contains(term))
                orderby n.Id descending
                select new { n.Id, n.Title, n.Description, CodePreview = n.Value.Substring(0, 220), n.Visibility, Author = u.DisplayName })
                .Take(max).ToListAsync(ct);
            results.AddRange(rows.Select(n => new DashboardContentItem("curl", n.Id, n.Title,
                Preview(n.Description), n.Author,
                n.Visibility == KnowledgeVisibility.Public, $"/wiki/curls/edit/{n.Id}",
                CodePreview: n.CodePreview)));
        }
        if (Include("report"))
        {
            var ids = LongIds("report");
            var rows = await (from n in db.MonitoringReports.AsNoTracking()
                join j in db.AutomatedJobs.AsNoTracking() on n.AutomatedJobId equals j.Id
                where (!filterById || ids.Contains(n.Id)) &&
                      (term == "" || n.Name.Contains(term) || n.ComparisonField.Contains(term) ||
                       n.ChartType.Contains(term) || n.MeasureFieldsJson.Contains(term) || j.Name.Contains(term))
                orderby n.UpdatedAtUtc descending, n.Id descending
                select new { n.Id, n.Name, n.ComparisonField, n.ChartType, JobName = j.Name })
                .Take(max).ToListAsync(ct);
            results.AddRange(rows.Select(n => new DashboardContentItem("report", n.Id, n.Name,
                Preview($"{n.JobName} · {n.ComparisonField}"), "گزارش", true, "/reports",
                ChartType: n.ChartType)));
        }
        var specialCards = new List<DashboardContentItem>();
        foreach (var special in DashboardSpecialCards.Titles)
        {
            if (!Include(special.Key) || (filterById && special.Key != "smart" && !wanted![special.Key].Contains(1)) ||
                (term.Length > 0 && !special.Value.Contains(term, StringComparison.OrdinalIgnoreCase) &&
                 !(special.Key is "event" or "events-day" or "events-week" or "events-month" && "رویدادها تقویم تولد".Contains(term))))
                continue;
            specialCards.Add(new DashboardContentItem(special.Key, 1, special.Value,
                special.Key == "smart" ? "نمایش محتوای منتخب" : "رویدادهای برنامه‌ریزی‌شده",
                "سامانه", true, "/team/calendar"));
        }
        results.InsertRange(0, specialCards);
        if (filterById && wanted!.TryGetValue("smart", out var smartIds))
        {
            // Each smart-card instance has its own persisted ID and configuration.
            var template = specialCards.FirstOrDefault(x => x.Type == "smart");
            if (template is not null)
            {
                results.RemoveAll(x => x.Type == "smart");
                results.AddRange(smartIds.Select(id => template with { Id = id }));
            }
        }
        // Apply an overall bound; the references resolver preserves persisted order after this call.
        return wanted is null ? results.Take(SearchLimit).ToList() : results;
    }

    private static string Preview(string? value, int limit = 220)
    {
        var text = (value ?? "").Replace('\r', ' ').Replace('\n', ' ').Trim();
        return text.Length <= limit ? text : text[..limit] + "…";
    }
}
