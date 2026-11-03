using System.Text.Json;

namespace NTSW_CENOPS.Application.Notes;

/// <summary>Versioned, headerless note table. Cells are plain text, never HTML.</summary>
public sealed class NoteTableData
{
    public List<List<string>> Rows { get; set; } = [ ["", ""], ["", ""] ];

    public static NoteTableData Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new();
        try
        {
            var table = JsonSerializer.Deserialize<NoteTableData>(json);
            return table is not null && Validate(table) ? table : new NoteTableData();
        }
        catch (JsonException) { return new NoteTableData(); }
    }

    public static bool IsValid(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return false;
        try { return Validate(JsonSerializer.Deserialize<NoteTableData>(json)); }
        catch (JsonException) { return false; }
    }

    private static bool Validate(NoteTableData? table)
    {
        if (table?.Rows is null || table.Rows.Count is < 1 or > 20) return false;
        var width = table.Rows[0]?.Count ?? 0;
        return width is >= 1 and <= 12 && table.Rows.All(r =>
            r is not null && r.Count == width && r.All(c => c is not null && c.Length <= 2000));
    }

    public string Serialize() => JsonSerializer.Serialize(this);
}
