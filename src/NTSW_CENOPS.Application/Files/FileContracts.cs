namespace NTSW_CENOPS.Application.Files;

/// <summary>A neutral, app-facing representation. UI and application modules never see FileStreaming types.</summary>
public sealed record AppFileInfo(Guid Id, string FileName, string ContentType, long Length, DateTimeOffset CreatedAtUtc, string? OwnerKey = null);

public sealed record FileCheckResult(bool IsValid, string? Message)
{
    public static FileCheckResult Valid() => new(true, null);
    public static FileCheckResult Reject(string message) => new(false, message);
}

public enum AppFilePreviewKind { Image, Pdf, Text, Spreadsheet, Unavailable, Video }
public sealed record AppFilePreviewSheet(string Name, IReadOnlyList<IReadOnlyList<string>> Rows);
public sealed record AppFilePreviewData(AppFilePreviewKind Kind, string? Text = null,
    IReadOnlyList<AppFilePreviewSheet>? Sheets = null, string? Message = null);

/// <summary>Host-side file preflight; implementation never reads bytes or touches storage.</summary>
public interface IFileCheckService
{
    FileCheckResult Check(string name, long length);
}

/// <summary>The sole entry point for UI, HTTP endpoints, and future app modules.</summary>
public interface IAppFileGateway
{
    long MaximumUploadBytes { get; }
    FileCheckResult Check(string name, long length);
    Task<AppFileInfo> UploadAsync(Stream stream, string name, string? contentType, CancellationToken ct = default, string? ownerKey = null);
    Task<AppFileInfo?> FindAsync(Guid id, CancellationToken ct = default);
    Task<AppFileRead> OpenReadAsync(Guid id, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
    Task<AppFilePreviewData> PreviewAsync(Guid id, CancellationToken ct = default);
}

/// <summary>Disposing a read result closes its underlying file handle.</summary>
public sealed record AppFileRead(AppFileInfo File, Stream Content) : IAsyncDisposable
{
    public ValueTask DisposeAsync() => Content.DisposeAsync();
}
