using NTSW_CENOPS.FileStreaming.Models;

namespace NTSW_CENOPS.FileStreaming.Contracts;

/// <summary>Reads bounded, read-only previews; does not mutate stored bytes.</summary>
public interface IFilePreviewReader
{
    Task<FilePreviewResult> ReadAsync(Guid id, CancellationToken cancellationToken = default);
}
