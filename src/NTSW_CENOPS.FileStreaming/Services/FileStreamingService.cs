using NTSW_CENOPS.FileStreaming.Contracts;
using NTSW_CENOPS.FileStreaming.Models;

namespace NTSW_CENOPS.FileStreaming.Services;

/// <summary>Orchestrates raw uploads; file processing and authorization are intentionally deferred.</summary>
public sealed class FileStreamingService : IFileStreamingService
{
    private readonly IFileStorage _storage;
    private readonly IFileMetadataStore _metadata;
    private readonly FileStreamingOptions _options;

    public FileStreamingService(IFileStorage storage, IFileMetadataStore metadata, FileStreamingOptions options)
    {
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        _metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _options.Validate();
    }

    public async Task<StoredFile> UploadAsync(FileUploadRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Content);

        var name = NormalizeDisplayName(request.FileName);
        var type = NormalizeContentType(request.ContentType);
        var id = Guid.NewGuid();
        var bytesSaved = await _storage.SaveAsync(id, request.Content, _options.MaximumUploadBytes,
            cancellationToken);
        var file = new StoredFile(id, name, type, bytesSaved, DateTimeOffset.UtcNow, request.OwnerKey);

        try
        {
            await _metadata.SaveAsync(file, cancellationToken);
        }
        catch
        {
            // A failed metadata write must not leave an apparently successful orphan upload.
            await _storage.DeleteAsync(id, CancellationToken.None);
            throw;
        }

        return file;
    }

    public Task<StoredFile?> FindAsync(Guid id, CancellationToken cancellationToken = default)
        => _metadata.FindAsync(id, cancellationToken);

    public async Task<FileReadResult> OpenReadAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var file = await _metadata.FindAsync(id, cancellationToken)
            ?? throw new FileNotFoundException("File does not exist.");
        var stream = await _storage.OpenReadAsync(id, cancellationToken);
        return new FileReadResult(file, stream);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var metadata = await _metadata.FindAsync(id, cancellationToken);
        if (metadata is null)
            return false;

        await _storage.DeleteAsync(id, cancellationToken);
        await _metadata.DeleteAsync(id, cancellationToken);
        return true;
    }

    private static string NormalizeDisplayName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("An original filename is required.", nameof(name));

        var trimmed = name.Replace('\\', '/').Split('/').Last().Trim();
        if (trimmed.Length == 0 || trimmed.Length > 255 || trimmed.Any(char.IsControl))
            throw new ArgumentException("Filename must be 1–255 printable characters.", nameof(name));
        return trimmed;
    }

    private static string NormalizeContentType(string? type)
    {
        if (string.IsNullOrWhiteSpace(type))
            return "application/octet-stream";
        var trimmed = type.Trim();
        if (trimmed.Length > 127 || trimmed.Any(char.IsControl))
            throw new ArgumentException("Invalid content type.", nameof(type));
        return trimmed; // Untrusted; content-type sniffing is reserved for the processing stage.
    }
}
