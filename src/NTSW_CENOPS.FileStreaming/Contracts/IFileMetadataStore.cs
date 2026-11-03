using NTSW_CENOPS.FileStreaming.Models;

namespace NTSW_CENOPS.FileStreaming.Contracts;

/// <summary>Metadata persistence can be replaced by SQL Server later.</summary>
public interface IFileMetadataStore
{
    Task SaveAsync(StoredFile file, CancellationToken cancellationToken = default);
    Task<StoredFile?> FindAsync(Guid id, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
