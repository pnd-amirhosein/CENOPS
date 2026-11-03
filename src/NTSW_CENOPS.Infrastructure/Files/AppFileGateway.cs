using NTSW_CENOPS.Application.Files;
using NTSW_CENOPS.FileStreaming.Contracts;
using NTSW_CENOPS.FileStreaming.Models;
using Microsoft.Extensions.Options;

namespace NTSW_CENOPS.Infrastructure.Files;

/// <summary>
/// The only adapter referencing FileStreaming. Callers depend exclusively on IAppFileGateway.
/// Basic preflight rules are deliberately separate from future content scanning/authorization.
/// </summary>
public sealed class AppFileGateway(IFileStreamingService files, IFileCheckService checker, IOptions<FileUploadPolicy> policy, IFilePreviewReader previewReader) : IAppFileGateway
{
    private readonly FileUploadPolicy _policy = policy.Value;

    public long MaximumUploadBytes => _policy.MaximumBytes;

    public FileCheckResult Check(string name, long length) => checker.Check(name, length);

    public async Task<AppFileInfo> UploadAsync(Stream stream, string name, string? contentType, CancellationToken ct = default, string? ownerKey = null)
    {
        ArgumentNullException.ThrowIfNull(stream);
        // A streamed upload may not support Length; byte counts are also enforced by FileStreaming.
        var advertisedLength = stream.CanSeek ? stream.Length - stream.Position : (long?)null;
        if (advertisedLength.HasValue)
        {
            var check = Check(name, advertisedLength.Value);
            if (!check.IsValid) throw new InvalidDataException(check.Message);
        }
        else if (!Check(name, 1).IsValid)
            throw new InvalidDataException("پسوند یا نام فایل معتبر نیست.");
        var stored = await files.UploadAsync(new FileUploadRequest(stream, name, contentType, ownerKey), ct);
        if (stored.Length == 0)
        {
            await files.DeleteAsync(stored.Id, CancellationToken.None);
            throw new InvalidDataException("فایل خالی قابل بارگذاری نیست.");
        }
        return Map(stored);
    }

    public async Task<AppFileInfo?> FindAsync(Guid id, CancellationToken ct = default)
    {
        var stored = await files.FindAsync(id, ct);
        return stored is null ? null : Map(stored);
    }

    public async Task<AppFileRead> OpenReadAsync(Guid id, CancellationToken ct = default)
    {
        var result = await files.OpenReadAsync(id, ct);
        return new AppFileRead(Map(result.File), result.Content);
    }

    public Task<bool> DeleteAsync(Guid id, CancellationToken ct = default) => files.DeleteAsync(id, ct);

    public async Task<AppFilePreviewData> PreviewAsync(Guid id, CancellationToken ct = default)
    {
        var preview = await previewReader.ReadAsync(id, ct);
        return new AppFilePreviewData((AppFilePreviewKind)preview.Kind, preview.Text,
            preview.Sheets?.Select(sheet => new AppFilePreviewSheet(sheet.Name, sheet.Rows)).ToArray(), preview.Message);
    }

    private static AppFileInfo Map(StoredFile f) => new(f.Id, f.FileName, f.ContentType, f.Length, f.CreatedAtUtc, f.OwnerKey);
}

/// <summary>Pre-upload limits; configured once by the host, independent of the storage engine.</summary>
public sealed class FileUploadPolicy
{
    public long MaximumBytes { get; set; } = 25L * 1024 * 1024;
    public string[] Extensions { get; set; } = [".png", ".jpg", ".jpeg", ".gif", ".webp", ".mp4", ".webm",
        ".pdf", ".txt", ".md", ".csv", ".tsv", ".json", ".xml", ".sql",
        ".cs", ".css", ".scss", ".html", ".htm", ".cshtml", ".razor", ".js", ".jsx", ".ts", ".tsx", ".svg",
        ".yaml", ".yml", ".log", ".config", ".ini", ".py", ".sh", ".ps1", ".diff", ".patch",
        ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".zip", ".7z"];
}
