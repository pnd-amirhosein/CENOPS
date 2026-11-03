using NTSW_CENOPS.Application.Files;
using Microsoft.Extensions.Options;

namespace NTSW_CENOPS.Infrastructure.Files;

/// <summary>Lightweight pre-upload checks only. Malware scanning/content inspection come later.</summary>
public sealed class FileCheckService(IOptions<FileUploadPolicy> settings) : IFileCheckService
{
    private readonly FileUploadPolicy _policy = settings.Value;

    public FileCheckResult Check(string name, long length)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 255 || name.Any(char.IsControl))
            return FileCheckResult.Reject("نام فایل معتبر نیست.");
        if (length <= 0)
            return FileCheckResult.Reject("فایل خالی قابل بارگذاری نیست.");
        if (length > _policy.MaximumBytes)
            return FileCheckResult.Reject($"حجم هر فایل نباید از {_policy.MaximumBytes / 1024 / 1024} مگابایت بیشتر باشد.");
        var extension = Path.GetExtension(name);
        if (string.IsNullOrEmpty(extension) || !_policy.Extensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            return FileCheckResult.Reject("پسوند این فایل مجاز نیست.");
        return FileCheckResult.Valid();
    }
}
