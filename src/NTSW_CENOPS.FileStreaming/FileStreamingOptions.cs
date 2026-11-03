namespace NTSW_CENOPS.FileStreaming;

/// <summary>Configuration for private, on-premises file storage.</summary>
public sealed class FileStreamingOptions
{
    /// <summary>Absolute path to a persistent directory OUTSIDE the web root and deployment output.</summary>
    public string RootDirectory { get; set; } = string.Empty;

    /// <summary>Maximum number of bytes allowed in a single upload (default: 25 MiB).</summary>
    public long MaximumUploadBytes { get; set; } = 25L * 1024 * 1024;

    internal void Validate()
    {
        if (string.IsNullOrWhiteSpace(RootDirectory) || !Path.IsPathFullyQualified(RootDirectory))
            throw new ArgumentException("An absolute private storage RootDirectory is required.", nameof(RootDirectory));

        if (MaximumUploadBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(MaximumUploadBytes), "Upload limit must be positive.");
    }
}
