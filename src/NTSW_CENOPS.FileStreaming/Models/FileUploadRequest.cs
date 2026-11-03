namespace NTSW_CENOPS.FileStreaming.Models;

/// <param name="Content">An open readable stream; its lifetime remains owned by the caller.</param>
/// <param name="FileName">The client-supplied display name, never a storage path.</param>
/// <param name="ContentType">Unverified client-declared MIME type. Content validation comes later.</param>
public sealed record FileUploadRequest(Stream Content, string FileName, string? ContentType = null, string? OwnerKey = null);
