namespace NTSW_CENOPS.FileStreaming.Exceptions;

public sealed class FileSizeLimitExceededException : IOException
{
    public FileSizeLimitExceededException(long maximumBytes)
        : base($"File exceeds the maximum allowed size of {maximumBytes} bytes.")
    {
        MaximumBytes = maximumBytes;
    }

    public long MaximumBytes { get; }
}
