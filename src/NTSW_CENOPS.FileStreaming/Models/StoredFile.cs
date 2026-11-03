namespace NTSW_CENOPS.FileStreaming.Models;

/// <summary>Metadata only; contents are stored separately and identified by an opaque ID.</summary>
public sealed record StoredFile(
    Guid Id,
    string FileName,
    string ContentType,
    long Length,
    DateTimeOffset CreatedAtUtc, string? OwnerKey = null);
