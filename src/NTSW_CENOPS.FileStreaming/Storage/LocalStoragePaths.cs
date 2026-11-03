namespace NTSW_CENOPS.FileStreaming.Storage;

/// <summary>Converts an opaque ID to deterministic, directory-sharded private paths.</summary>
internal sealed class LocalStoragePaths
{
    private readonly string _root;

    public LocalStoragePaths(FileStreamingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        _root = Path.GetFullPath(options.RootDirectory);
    }

    public string ForObject(Guid id) => GetPath("objects", id, string.Empty);
    public string ForMetadata(Guid id) => GetPath("metadata", id, ".json");

    private string GetPath(string area, Guid id, string extension)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("A valid file ID is required.", nameof(id));

        var name = id.ToString("N");
        return Path.Combine(_root, area, name[..2], name + extension);
    }
}
