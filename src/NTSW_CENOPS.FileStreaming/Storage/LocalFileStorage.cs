using System.Buffers;
using NTSW_CENOPS.FileStreaming.Contracts;
using NTSW_CENOPS.FileStreaming.Exceptions;

namespace NTSW_CENOPS.FileStreaming.Storage;

/// <summary>
/// Writes uploads to a private directory using an atomic publish operation.
/// Filenames and client-provided paths are never used to construct storage paths.
/// </summary>
public sealed class LocalFileStorage : IFileStorage
{
    private const int BufferSize = 80 * 1024;
    private readonly LocalStoragePaths _paths;

    public LocalFileStorage(FileStreamingOptions options) => _paths = new LocalStoragePaths(options);

    public async Task<long> SaveAsync(Guid id, Stream source, long maximumBytes,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!source.CanRead)
            throw new ArgumentException("Upload stream must be readable.", nameof(source));
        if (maximumBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(maximumBytes));

        var destination = _paths.ForObject(id);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var temporary = destination + "." + Guid.NewGuid().ToString("N") + ".upload";
        var buffer = ArrayPool<byte>.Shared.Rent(BufferSize);

        try
        {
            long total = 0;
            await using (var target = new FileStream(temporary, new FileStreamOptions
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                Share = FileShare.None,
                Options = FileOptions.Asynchronous,
                BufferSize = BufferSize
            }))
            {
                int read;
                while ((read = await source.ReadAsync(buffer.AsMemory(0, BufferSize), cancellationToken)) != 0)
                {
                    if (total > maximumBytes - read)
                        throw new FileSizeLimitExceededException(maximumBytes);

                    await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    total += read;
                }

                await target.FlushAsync(cancellationToken);
            }

            // Publishing only after the stream completes keeps partial uploads invisible.
            File.Move(temporary, destination);
            return total;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
            if (File.Exists(temporary))
                File.Delete(temporary);
        }
    }

    public Task<Stream> OpenReadAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = _paths.ForObject(id);
        Stream stream = new FileStream(path, new FileStreamOptions
        {
            Mode = FileMode.Open,
            Access = FileAccess.Read,
            Share = FileShare.Read,
            Options = FileOptions.Asynchronous | FileOptions.SequentialScan
        });
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        File.Delete(_paths.ForObject(id)); // Safe when already missing.
        return Task.CompletedTask;
    }
}
