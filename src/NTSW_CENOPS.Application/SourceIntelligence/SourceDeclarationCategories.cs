namespace NTSW_CENOPS.Application.SourceIntelligence;

/// <summary>
/// Stable categories shared by the index query and UI. A category describes a declaration's
/// Roslyn symbol kind; it never represents a reference or inferred domain entity.
/// </summary>
public static class SourceDeclarationCategories
{
    public const string Types = "types";
    public const string Methods = "methods";
    public const string Properties = "properties";
    public const string Fields = "fields";
    public const string Locals = "locals";
    public const string Parameters = "parameters";
    public const string Other = "other";

    public static readonly IReadOnlyList<string> Order =
        [Types, Methods, Properties, Fields, Locals, Parameters, Other];

    public static string ForKind(string kind) => kind switch
    {
        "Class" or "Record" or "RecordStruct" or "Struct" or "Interface" or "Enum" or "Delegate" => Types,
        "Method" or "Constructor" => Methods,
        "Property" => Properties,
        "Field" or "Event" or "EnumMember" => Fields,
        "Variable" => Locals,
        "Parameter" or "TypeParameter" => Parameters,
        _ => Other
    };

    public static string Label(string category) => category switch
    {
        Types => "انواع (کلاس، اینترفیس، Enum و ...)",
        Methods => "متدها و سازنده‌ها",
        Properties => "پراپرتی‌ها",
        Fields => "فیلدها و رویدادها",
        Locals => "متغیرهای محلی",
        Parameters => "پارامترها",
        Other => "سایر تعریف‌ها",
        _ => "همهٔ تعریف‌ها"
    };
}
