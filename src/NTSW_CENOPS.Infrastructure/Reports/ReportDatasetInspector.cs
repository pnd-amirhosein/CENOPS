using System.Globalization;
using System.Text.Json;
using NTSW_CENOPS.Application.Reports;

namespace NTSW_CENOPS.Infrastructure.Reports;

/// <summary>
/// Validates a job's most recently collected window. The daily document is an array of
/// { windowStartUtc, windowEndUtc, collectedAtUtc, rows: [...] } entries.
/// Never executes the job SQL and never returns row values to a browser.
/// </summary>
internal static class ReportDatasetInspector
{
    private const int MaxFields = 64;

    public static ReportJobInspection Inspect(string dailyJson, DateOnly dataDate)
    {
        try
        {
            using var document = JsonDocument.Parse(dailyJson);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                return ReportJobInspection.Empty("ساختار داده ذخیره‌شده معتبر نیست.");

            // A later collected window is authoritative; do not silently fall back to old data.
            JsonElement? latest = null;
            DateTimeOffset latestEnd = DateTimeOffset.MinValue;
            DateTimeOffset latestCollected = DateTimeOffset.MinValue;
            foreach (var window in document.RootElement.EnumerateArray())
            {
                if (window.ValueKind != JsonValueKind.Object ||
                    !window.TryGetProperty("rows", out var rows) || rows.ValueKind != JsonValueKind.Array)
                    return ReportJobInspection.Empty("ساختار پنجره‌های داده معتبر نیست.");
                var end = ReadTime(window, "windowEndUtc");
                var collected = ReadTime(window, "collectedAtUtc");
                if (latest is null || end > latestEnd || (end == latestEnd && collected > latestCollected))
                {
                    latest = rows;
                    latestEnd = end;
                    latestCollected = collected;
                }
            }

            if (latest is null || latest.Value.GetArrayLength() == 0)
                return ReportJobInspection.Empty("آخرین اجرای ذخیره‌شده نتیجه‌ای ندارد. پس از جمع‌آوری داده دوباره تلاش کنید.");

            var types = new Dictionary<string, FieldKind>(StringComparer.Ordinal);
            var rowsSeen = 0;
            foreach (var row in latest.Value.EnumerateArray())
            {
                if (row.ValueKind != JsonValueKind.Object)
                    return Invalid("هر ردیف نتیجه باید یک شیء JSON باشد.", dataDate, rowsSeen);
                rowsSeen++;
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (var property in row.EnumerateObject())
                {
                    if (!names.Add(property.Name))
                        return Invalid("نام ستون تکراری در نتیجه وجود دارد.", dataDate, rowsSeen);
                    if (!types.ContainsKey(property.Name))
                    {
                        if (types.Count >= MaxFields)
                            return Invalid("نتیجه بیش از ۶۴ ستون دارد و برای گزارش مناسب نیست.", dataDate, rowsSeen);
                        // A newly appearing column in a later row means earlier rows lacked it.
                        types[property.Name] = rowsSeen == 1 ? FieldKind.Unknown : FieldKind.Missing;
                    }
                    types[property.Name] = Combine(types[property.Name], Detect(property.Value));
                }
                // Missing a field is a schema mismatch, not a zero.
                foreach (var key in types.Keys.ToArray())
                    if (!names.Contains(key)) types[key] = FieldKind.Missing;
            }

            var fields = types.Select(x => new ReportFieldInfo(x.Key, TypeLabel(x.Value),
                x.Value is FieldKind.Number or FieldKind.DateTime,
                x.Value == FieldKind.Number)).ToArray();

            var invalidFields = fields.Where(x => x.Type is not ("number" or "datetime")).ToArray();
            if (invalidFields.Length != 0)
                return new(true, false,
                    $"ستون‌های ناسازگار: {string.Join("، ", invalidFields.Take(6).Select(x => x.Name))}. فقط یک ستون تاریخ/زمان و سایر ستون‌های عددی پذیرفته می‌شوند.",
                    fields, dataDate, rowsSeen);

            var dateFields = fields.Where(x => x.Type == "datetime").ToArray();
            var numerical = fields.Count(x => x.IsNumeric);
            // With a date dimension, numeric columns are measures, not comparison choices.
            // Otherwise a numeric comparison must leave at least one numeric measure.
            if (dateFields.Length == 1)
                fields = fields.Select(x => x with { CanCompare = x.Type == "datetime" }).ToArray();
            if (dateFields.Length > 1 || numerical < 1 || (dateFields.Length == 0 && numerical < 2))
                return new(true, false,
                    "نتیجه باید حداقل یک مقدار عددی و یک پارامتر مقایسه عددی یا تاریخ/زمان داشته باشد؛ فقط یک ستون تاریخ/زمان مجاز است.",
                    fields, dataDate, rowsSeen);

            return new(true, true,
                $"ساختار آخرین پنجره معتبر است: {rowsSeen:N0} ردیف و {fields.Length} ستون.",
                fields, dataDate, rowsSeen);
        }
        catch (JsonException)
        {
            return ReportJobInspection.Empty("داده JSON ذخیره‌شده قابل خواندن نیست.");
        }
    }

    private static DateTimeOffset ReadTime(JsonElement element, string key) =>
        element.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String &&
        DateTimeOffset.TryParse(value.GetString(), CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind, out var parsed)
            ? parsed.ToUniversalTime() : DateTimeOffset.MinValue;

    private static ReportJobInspection Invalid(string message, DateOnly date, int count) =>
        new(true, false, message, Array.Empty<ReportFieldInfo>(), date, count);

    private enum FieldKind { Unknown, Number, DateTime, Text, Unsupported, Missing, Mixed }

    private static FieldKind Detect(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Null => FieldKind.Unknown,
        JsonValueKind.Number => FieldKind.Number,
        JsonValueKind.String when DateTimeOffset.TryParse(value.GetString(), CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind, out _) => FieldKind.DateTime,
        JsonValueKind.String => FieldKind.Text,
        _ => FieldKind.Unsupported
    };

    private static FieldKind Combine(FieldKind existing, FieldKind incoming)
    {
        if (existing == FieldKind.Missing || incoming == FieldKind.Missing) return FieldKind.Missing;
        if (incoming == FieldKind.Unknown) return existing;
        if (existing == FieldKind.Unknown) return incoming;
        return existing == incoming ? existing : FieldKind.Mixed;
    }

    private static string TypeLabel(FieldKind kind) => kind switch
    {
        FieldKind.Number => "number", FieldKind.DateTime => "datetime",
        FieldKind.Text => "text", FieldKind.Unknown => "unknown",
        FieldKind.Missing => "missing", FieldKind.Mixed => "mixed", _ => "unsupported"
    };
}
