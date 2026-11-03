using NTSW_CENOPS.FileStreaming.Models;

namespace NTSW_CENOPS.FileStreaming.Contracts;

/// <summary>
/// Internal file operations, intentionally not an HTTP endpoint or authorization boundary.
/// A future application layer must authorize callers before exposing file reads.
/// </summary>
public interface IFileStreamingService
{
    Task<StoredFile> UploadAsync(FileUploadRequest request, CancellationToken cancellationToken = default);
    Task<StoredFile?> FindAsync(Guid id, CancellationToken cancellationToken = default);
    Task<FileReadResult> OpenReadAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
