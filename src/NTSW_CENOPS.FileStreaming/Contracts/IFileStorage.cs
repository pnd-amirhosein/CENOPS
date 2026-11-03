namespace NTSW_CENOPS.FileStreaming.Contracts;

/// <summary>Byte storage only. A future S3 adapter can implement this contract.</summary>
public interface IFileStorage
{
    Task<long> SaveAsync(Guid id, Stream source, long maximumBytes, CancellationToken cancellationToken = default);
    Task<Stream> OpenReadAsync(Guid id, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
