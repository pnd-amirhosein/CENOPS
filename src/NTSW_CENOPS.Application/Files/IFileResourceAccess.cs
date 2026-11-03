using System.Security.Claims;

namespace NTSW_CENOPS.Application.Files;

/// <summary>Authorization requires both caller access to the parent and actual attachment membership.</summary>
public interface IFileResourceAccess
{
    Task<bool> CanReadAsync(Guid fileId, long? messageId, int? noteId, ClaimsPrincipal actor,
        CancellationToken ct = default);

    // Only freshly uploaded, unreferenced objects may be physically deleted by their uploader.
    Task<bool> CanDeleteUnattachedAsync(Guid fileId, ClaimsPrincipal actor, CancellationToken ct = default);
}
