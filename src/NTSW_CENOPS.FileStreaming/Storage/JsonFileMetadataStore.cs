using System.Text.Json;
using NTSW_CENOPS.FileStreaming.Contracts;
using NTSW_CENOPS.FileStreaming.Models;

namespace NTSW_CENOPS.FileStreaming.Storage;

/// <summary>
/// Minimal durable metadata store for the first iteration. Replace with SQL-backed
/// IFileMetadataStore when introducing ownership and attachment relationships.
/// </summary>
public sealed class JsonFileMetadataStore : IFileMetadataStore
{
    private readonly LocalStoragePaths _paths;

    public JsonFileMetadataStore(FileStreamingOptions options) => _paths = new LocalStoragePaths(options);

    public async Task SaveAsync(StoredFile file, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);
        var path = _paths.ForMetadata(file.Id);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var stream = new FileStream(temporary, new FileStreamOptions
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                Share = FileShare.None,
                Options = FileOptions.Asynchronous
            }))
            {
                await JsonSerializer.SerializeAsync(stream, file, cancellationToken: cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }
            File.Move(temporary, path);
        }
        finally
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
        }
    }

    public async Task<StoredFile?> FindAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var path = _paths.ForMetadata(id);
        if (!File.Exists(path))
            return null;

        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
            FileShare.Read, bufferSize: 4096, useAsync: true);
        var file = await JsonSerializer.DeserializeAsync<StoredFile>(stream,
            cancellationToken: cancellationToken);
        if (file is null || file.Id != id)
            throw new InvalidDataException("Stored file metadata is invalid.");
        return file;
    }

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        File.Delete(_paths.ForMetadata(id));
        return Task.CompletedTask;
    }
}
