using System.Security.Claims;
using NTSW_CENOPS.Application.Notes;
using System.Text.Json.Serialization;

namespace NTSW_CENOPS.Application.Dashboards;

/// <summary>
/// Content identity and editable geometry. w/h are retained to read layouts saved before 2.19.
/// Pixel geometry is measured from the *right* edge of a canvas cw pixels wide (RTL).
/// An all-zero pixel geometry denotes a legacy/unplaced card initialized by the browser.
/// </summary>
public sealed record DashboardContentReference(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("w")] int Width = 0,
    [property: JsonPropertyName("h")] int Height = 0,
    [property: JsonPropertyName("x")] int X = 0,
    [property: JsonPropertyName("y")] int Y = 0,
    [property: JsonPropertyName("pw")] int PixelWidth = 0,
    [property: JsonPropertyName("ph")] int PixelHeight = 0,
    [property: JsonPropertyName("cw")] int CanvasWidth = 0,
    [property: JsonPropertyName("options")] DashboardCardOptions? Options = null)
{
    public DashboardContentReference Normalized() => this with
    {
        Width = Width == 0 ? (Type is "announcement" or "sql" or "curl" or "report" or "smart" or "events-day" or "events-week" or "events-month" ? 3 : 2) : Width,
        Height = Height == 0 ? (Type is "report" or "smart" or "events-day" or "events-week" or "events-month" ? 6 : Type is "sql" or "curl" or "todo" ? 5 : Type == "announcement" ? 2 : 4) : Height
    };
}

/// <summary>Saved with the card; no private document data is stored inside the layout.</summary>
public sealed record DashboardCardOptions(
    [property: JsonPropertyName("eventId")] int? EventId = null,
    [property: JsonPropertyName("items")] IReadOnlyList<DashboardContentReference>? Items = null,
    [property: JsonPropertyName("horizontal")] bool Horizontal = true,
    [property: JsonPropertyName("intervalSeconds")] int IntervalSeconds = 3);

public static class DashboardSpecialCards
{
    public static readonly IReadOnlyDictionary<string, string> Titles = new Dictionary<string, string>
    {
        ["event"] = "رویداد انتخابی", ["events-day"] = "رویدادهای امروز",
        ["events-week"] = "رویدادهای این هفته", ["events-month"] = "رویدادهای این ماه",
        ["smart"] = "کارت هوشمند"
    };
    public static bool IsSpecial(string type) => Titles.ContainsKey(type);
    public static bool IsEmbeddable(string type) => type is "note" or "announcement" or "todo" or "sql" or "curl" or "report" or "events-day" or "events-week" or "events-month";
}

public sealed record DashboardContentItem(
    string Type, long Id, string Title, string Summary, string Author,
    bool IsPublic, string Url, string? CodePreview = null,
    IReadOnlyList<DashboardTodoPreviewItem>? TodoItems = null,
    string? ChartType = null, bool IsRestricted = false,
    IReadOnlyList<NoteBlockModel>? NoteBlocks = null);

/// <summary>Small, read-only task snapshot used in picker cards.</summary>
public sealed record DashboardTodoPreviewItem(string Text, bool IsCompleted);

public interface IDashboardContentService
{
    Task<IReadOnlyList<DashboardContentItem>> SearchAsync(
        string? query, string? category, bool publicOnly, ClaimsPrincipal actor, CancellationToken ct = default);

    Task<IReadOnlyList<DashboardContentItem>> ResolveAsync(
        IReadOnlyList<DashboardContentReference> references, bool publicOnly,
        ClaimsPrincipal actor, CancellationToken ct = default);
}

public sealed record DashboardContentPickerResult(bool Global, IReadOnlyList<DashboardContentReference> Items);
