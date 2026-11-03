using System.Text.Json;

namespace NTSW_CENOPS.Application.Files;

/// <summary>File blocks persist only IDs, never URLs, bytes, or local paths.</summary>
public static class FileReferenceCodec
{
    public static IReadOnlyList<Guid> Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return [];
        try
        {
            var ids = JsonSerializer.Deserialize<List<Guid>>(value);
            if (ids is null || ids.Count > 40 || ids.Any(x => x == Guid.Empty) || ids.Distinct().Count() != ids.Count)
                return [];
            return ids;
        }
        catch (JsonException) { return []; }
    }

    public static string Serialize(IEnumerable<Guid> ids) => JsonSerializer.Serialize(ids);

    public static bool IsValid(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return true;
        var ids = Parse(value);
        return ids.Count > 0 && Serialize(ids) is { Length: <= 8000 };
    }
}
