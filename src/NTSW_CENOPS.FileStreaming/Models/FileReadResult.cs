namespace NTSW_CENOPS.FileStreaming.Models;

/// <summary>Caller must dispose the returned file stream after reading.</summary>
public sealed record FileReadResult(StoredFile File, Stream Content) : IAsyncDisposable
{
    public ValueTask DisposeAsync() => Content.DisposeAsync();
}
