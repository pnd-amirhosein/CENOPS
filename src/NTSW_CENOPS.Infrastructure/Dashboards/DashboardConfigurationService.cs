using System.Security.Claims;
using System.Text.Json;
using NTSW_CENOPS.Application.Access;
using NTSW_CENOPS.Application.Dashboards;
using NTSW_CENOPS.Domain.Authorization;
using NTSW_CENOPS.Infrastructure.Knowledge;
using NTSW_CENOPS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace NTSW_CENOPS.Infrastructure.Dashboards;

/// <summary>
/// Separates shared content from personal dashboards. All mutations validate identity and ownership
/// again at the service boundary, even when the UI has hidden the relevant controls.
/// </summary>
public sealed class DashboardConfigurationService(IDbContextFactory<AppDbContext> factory, IAccessService access,
    IDashboardContentService content)
    : IDashboardConfigurationService
{
    private const int MaxLayoutCharacters = 128_000;

    private async Task<int> RequireUserAsync(ClaimsPrincipal actor, CancellationToken ct)
    {
        var id = DocumentAccess.ActorId(actor);
        if (id == 0 || !await access.HasAccessAsync(actor, AccessKeys.Dashboard, ct))
            throw new UnauthorizedAccessException("مجوز مشاهده داشبورد را ندارید.");
        return id;
    }

    private static async Task RequireManagerAsync(AppDbContext db, int userId, CancellationToken ct)
    {
        if (!await DocumentAccess.IsManagerAsync(db, userId, ct))
            throw new UnauthorizedAccessException("تنها مدیر یا مالک سامانه می‌تواند داشبورد عمومی را ویرایش کند.");
    }

    private static string ValidName(string? value, int maxLength, string label)
    {
        var normalized = (value ?? "").Trim();
        if (normalized.Length is < 2 || normalized.Length > maxLength)
            throw new ArgumentException($"{label} باید بین ۲ تا {maxLength} نویسه باشد.");
        return normalized;
    }

    private static string ValidLayout(string? layout)
    {
        if (string.IsNullOrWhiteSpace(layout) || layout.Length > MaxLayoutCharacters)
            throw new ArgumentException("ساختار داشبورد خالی یا بیش از حد بزرگ است.");
        using var json = JsonDocument.Parse(layout);
        if (json.RootElement.ValueKind != JsonValueKind.Array)
            throw new ArgumentException("چیدمان داشبورد باید یک آرایه JSON باشد.");
        return layout;
    }

    private async Task<string> VerifiedLayoutAsync(string raw, string? previousLayout, bool isGlobal, ClaimsPrincipal actor, CancellationToken ct)
    {
        var layout = ValidLayout(raw);
        List<DashboardContentReference> references;
        try
        {
            // Reject unknown fields and malformed references rather than persisting arbitrary JSON.
            using var doc = JsonDocument.Parse(layout);
            references = new List<DashboardContentReference>();
            foreach (var element in doc.RootElement.EnumerateArray())
            {
                if (element.ValueKind != JsonValueKind.Object ||
                    element.EnumerateObject().Any(p => p.Name is not ("type" or "id" or "w" or "h" or "x" or "y" or "pw" or "ph" or "cw" or "options")) ||
                    !element.TryGetProperty("type", out var type) || type.ValueKind != JsonValueKind.String ||
                    !element.TryGetProperty("id", out var id) || !id.TryGetInt64(out var contentId))
                    throw new ArgumentException("ساختار یکی از کارت‌های داشبورد معتبر نیست.");
                var width = element.TryGetProperty("w", out var w) ? w.GetInt32() : 0;
                var height = element.TryGetProperty("h", out var h) ? h.GetInt32() : 0;
                var x = element.TryGetProperty("x", out var gx) ? gx.GetInt32() : 0;
                var y = element.TryGetProperty("y", out var gy) ? gy.GetInt32() : 0;
                var pixelWidth = element.TryGetProperty("pw", out var gpw) ? gpw.GetInt32() : 0;
                var pixelHeight = element.TryGetProperty("ph", out var gph) ? gph.GetInt32() : 0;
                var canvasWidth = element.TryGetProperty("cw", out var gcw) ? gcw.GetInt32() : 0;
                DashboardCardOptions? options = null;
                if (element.TryGetProperty("options", out var optionElement) && optionElement.ValueKind != JsonValueKind.Null)
                {
                    // Do not trust a nested layout supplied by the browser. Only a flat,
                    // permission-checked selection is accepted for rotating cards.
                    if (optionElement.ValueKind != JsonValueKind.Object ||
                        optionElement.EnumerateObject().Any(p => p.Name is not ("eventId" or "items" or "horizontal" or "intervalSeconds")))
                        throw new ArgumentException("تنظیمات کارت نامعتبر است.");
                    options = optionElement.Deserialize<DashboardCardOptions>();
                    if (options is null || options.IntervalSeconds is < 1 or > 3600 ||
                        (options.Items?.Count ?? 0) > 30 ||
                        (options.Items?.Any(c => c.Id <= 0 || !DashboardSpecialCards.IsEmbeddable(c.Type) ||
                             c.Options is not null || c.PixelWidth != 0 || c.CanvasWidth != 0 || c.PixelHeight != 0) ?? false))
                        throw new ArgumentException("تنظیمات یا تعداد محتوای کارت نامعتبر است.");
                    if (options.Items is { } nested && nested.Select(c => (c.Type, c.Id)).Distinct().Count() != nested.Count)
                        throw new ArgumentException("محتوای تکراری در کارت هوشمند مجاز نیست.");
                }
                var cardType = type.GetString() ?? "";
                if (options is not null && (cardType is not ("smart" or "event") ||
                    (cardType == "event" && (options.Items is { Count: > 0 } || options.EventId is <= 0)) ||
                    (cardType == "smart" && options.EventId.HasValue)))
                    throw new ArgumentException("این کارت از تنظیمات پشتیبانی نمی‌کند.");
                var reference = new DashboardContentReference(cardType, contentId,
                    width, height, x, y, pixelWidth, pixelHeight, canvasWidth, options).Normalized();
                if (reference.Width is < 1 or > 6 || reference.Height is < 2 or > 12)
                    throw new ArgumentException("ابعاد کارت معتبر نیست.");
                // Reject half-defined geometry and unbounded/off-canvas coordinates at the service boundary.
                var legacy = pixelWidth == 0 && pixelHeight == 0 && canvasWidth == 0 && x == 0 && y == 0;
                var positioned = canvasWidth is >= 200 and <= 10000 &&
                    pixelWidth is >= 40 and <= 10000 && pixelHeight is >= 40 and <= 10000 &&
                    x >= 0 && (long)x + pixelWidth <= canvasWidth + 1 && y is >= 0 and <= 1000000;
                if (!legacy && !positioned)
                    throw new ArgumentException("موقعیت یا اندازه کارت معتبر نیست.");
                references.Add(reference);
            }
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or FormatException or OverflowException)
        {
            throw new ArgumentException("ساختار کارت‌های داشبورد معتبر نیست.", e);
        }
        if (references.Count > DashboardContentService.MaxItems)
            throw new ArgumentException("حداکثر ۶۰ کارت در هر بخش قابل ثبت است.");
        // References already stored in this very dashboard may become private/deleted
        // later. They are safe to retain: resolution redacts them for unauthorized
        // viewers, and the saved JSON contains references only, never actual content.
        // A NEW inaccessible reference remains forbidden (including nested smart slides).
        List<DashboardContentReference> previous;
        try { previous = JsonSerializer.Deserialize<List<DashboardContentReference>>(previousLayout ?? "[]") ?? []; }
        catch (JsonException) { previous = []; }
        var previousKeys = previous.Select(c => (c.Type, c.Id)).ToHashSet();
        var resolved = await content.ResolveAsync(references, isGlobal, actor, ct);
        if (resolved.Any(item => item.IsRestricted && !previousKeys.Contains((item.Type, item.Id))))
            throw new UnauthorizedAccessException("محتوای جدید باید هنگام افزودن برای شما قابل دسترسی باشد.");
        foreach (var card in references.Where(c => c.Type == "smart" && c.Options?.Items is { Count: > 0 }))
        {
            var embedded = await content.ResolveAsync(card.Options!.Items!, isGlobal, actor, ct);
            var oldSlides = previous.FirstOrDefault(x => x.Type == card.Type && x.Id == card.Id)?.Options?.Items;
            var oldSlideKeys = (oldSlides ?? []).Select(x => (x.Type, x.Id)).ToHashSet();
            if (embedded.Any(item => item.IsRestricted && !oldSlideKeys.Contains((item.Type, item.Id))))
                throw new UnauthorizedAccessException("محتوای جدید کارت هوشمند قابل دسترسی نیست.");
        }
        // Global dashboards only accept new PUBLIC items; existing references stay
        // to avoid destroying the layout when an owner later makes an item private.
        if (isGlobal && resolved.Any(item => !item.IsRestricted && !item.IsPublic &&
             !previousKeys.Contains((item.Type, item.Id))))
            throw new UnauthorizedAccessException("فقط محتوای عمومی را می‌توان به داشبورد عمومی افزود.");
        return JsonSerializer.Serialize(references);
    }

    public async Task<DashboardWorkspaceModel> GetWorkspaceAsync(ClaimsPrincipal actor, CancellationToken ct = default)
    {
        var actorId = await RequireUserAsync(actor, ct);
        await using var db = await factory.CreateDbContextAsync(ct);
        var global = await db.GlobalDashboardConfigurations.AsNoTracking().SingleOrDefaultAsync(x => x.Id == 1, ct);
        // All user dashboards are browseable. A viewer can modify only those they created.
        // IsDefault is based on this viewer's preference, not on the dashboard creator's setting.
        var dashboards = await (from d in db.UserDashboardConfigurations.AsNoTracking()
            join u in db.Users.AsNoTracking() on d.UserId equals u.Id
            orderby d.UpdatedAtUtc descending, d.Id descending
            select new { d.Id, d.UserId, d.Name, d.LayoutJson, d.IsDefault, d.UpdatedAtUtc, OwnerName = u.DisplayName })
            .ToListAsync(ct);
        var preference = await db.DashboardViewerPreferences.AsNoTracking()
            .Where(x => x.UserId == actorId).Select(x => (int?)x.DashboardId).SingleOrDefaultAsync(ct);
        var ownDefault = dashboards.FirstOrDefault(x => x.UserId == actorId && x.IsDefault)?.Id;
        var selected = preference is int stored && dashboards.Any(x => x.Id == stored) ? stored
            : ownDefault ?? dashboards.FirstOrDefault(x => x.UserId == actorId)?.Id ?? dashboards.FirstOrDefault()?.Id ?? 0;
        var userDashboards = dashboards.OrderByDescending(x => x.Id == selected)
            .ThenByDescending(x => x.UserId == actorId).ThenBy(x => x.Name)
            .Select(x => new UserDashboardModel(x.Id, x.Name, x.LayoutJson, x.Id == selected,
                x.UpdatedAtUtc, x.UserId, x.OwnerName, x.UserId == actorId)).ToList();
        var canEditGlobal = await DocumentAccess.IsManagerAsync(db, actorId, ct);
        return new DashboardWorkspaceModel(
            new DashboardGlobalModel("داشبورد عمومی", global?.LayoutJson ?? "[]", global?.UpdatedAtUtc),
            userDashboards, selected, canEditGlobal);
    }

    public async Task<UserDashboardModel> CreatePersonalAsync(string name, ClaimsPrincipal actor, CancellationToken ct = default)
    {
        var actorId = await RequireUserAsync(actor, ct);
        var title = ValidName(name, 120, "نام داشبورد");
        await using var db = await factory.CreateDbContextAsync(ct);
        var hasAny = await db.UserDashboardConfigurations.AnyAsync(x => x.UserId == actorId, ct);
        var now = DateTimeOffset.UtcNow;
        var item = new UserDashboardConfiguration
        {
            UserId = actorId, Name = title, LayoutJson = "[]", IsDefault = !hasAny, UpdatedAtUtc = now
        };
        db.UserDashboardConfigurations.Add(item);
        await db.SaveChangesAsync(ct);
        return new UserDashboardModel(item.Id, item.Name, item.LayoutJson, item.IsDefault, item.UpdatedAtUtc, actorId, "", true);
    }

    public async Task SavePersonalAsync(int id, string name, string layoutJson, ClaimsPrincipal actor, CancellationToken ct = default)
    {
        var actorId = await RequireUserAsync(actor, ct);
        var title = ValidName(name, 120, "نام داشبورد");
        await using var db = await factory.CreateDbContextAsync(ct);
        var item = await db.UserDashboardConfigurations.SingleOrDefaultAsync(x => x.Id == id && x.UserId == actorId, ct)
            ?? throw new UnauthorizedAccessException("داشبورد شخصی یافت نشد یا به شما تعلق ندارد.");
        var layout = await VerifiedLayoutAsync(layoutJson, item.LayoutJson, false, actor, ct);
        item.Name = title;
        item.LayoutJson = layout;
        item.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task SaveGlobalAsync(string title, string layoutJson, ClaimsPrincipal actor, CancellationToken ct = default)
    {
        var actorId = await RequireUserAsync(actor, ct);
        // The shared dashboard title is a fixed system label, not an editable setting.
        await using var db = await factory.CreateDbContextAsync(ct);
        await RequireManagerAsync(db, actorId, ct);
        var item = await db.GlobalDashboardConfigurations.SingleOrDefaultAsync(x => x.Id == 1, ct);
        var layout = await VerifiedLayoutAsync(layoutJson, item?.LayoutJson, true, actor, ct);
        if (item is null)
        {
            item = new GlobalDashboardConfiguration { Id = 1 };
            db.GlobalDashboardConfigurations.Add(item);
        }
        item.Title = "داشبورد عمومی";
        item.LayoutJson = layout;
        item.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task DeletePersonalAsync(int id, ClaimsPrincipal actor, CancellationToken ct = default)
    {
        var actorId = await RequireUserAsync(actor, ct);
        await using var db = await factory.CreateDbContextAsync(ct);
        var item = await db.UserDashboardConfigurations.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new KeyNotFoundException("داشبورد پیدا نشد.");
        if (item.UserId != actorId && !await DocumentAccess.IsManagerAsync(db, actorId, ct))
            throw new UnauthorizedAccessException("فقط سازنده یا مدیر می‌تواند داشبورد را حذف کند.");
        // Restrict FK: clear viewer preferences first, then delete the dashboard.
        // Readers will fall back to their own/another shared dashboard on next load.
        var preferences = await db.DashboardViewerPreferences.Where(x => x.DashboardId == id).ToListAsync(ct);
        db.DashboardViewerPreferences.RemoveRange(preferences);
        db.UserDashboardConfigurations.Remove(item);
        await db.SaveChangesAsync(ct);
    }

    public async Task SetDefaultAsync(int id, ClaimsPrincipal actor, CancellationToken ct = default)
    {
        var actorId = await RequireUserAsync(actor, ct);
        await using var db = await factory.CreateDbContextAsync(ct);
        if (!await db.UserDashboardConfigurations.AnyAsync(x => x.Id == id, ct))
            throw new ArgumentException("داشبورد انتخاب‌شده یافت نشد.");
        var preference = await db.DashboardViewerPreferences.SingleOrDefaultAsync(x => x.UserId == actorId, ct);
        if (preference is null)
            db.DashboardViewerPreferences.Add(new DashboardViewerPreference { UserId = actorId, DashboardId = id });
        else preference.DashboardId = id;
        await db.SaveChangesAsync(ct);
    }
}
