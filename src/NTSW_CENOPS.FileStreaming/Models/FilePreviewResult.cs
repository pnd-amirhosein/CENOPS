namespace NTSW_CENOPS.FileStreaming.Models;

/// <summary>Display-only data. Executable content is always treated as plain text.</summary>
public enum FilePreviewKind { Image, Pdf, Text, Spreadsheet, Unavailable, Video }

public sealed record FilePreviewSheet(string Name, IReadOnlyList<IReadOnlyList<string>> Rows);

public sealed record FilePreviewResult(
    FilePreviewKind Kind,
    string? Text = null,
    IReadOnlyList<FilePreviewSheet>? Sheets = null,
    string? Message = null);
