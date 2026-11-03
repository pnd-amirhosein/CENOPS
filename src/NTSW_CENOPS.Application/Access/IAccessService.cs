using System.Security.Claims;
using NTSW_CENOPS.Application.Administration;

namespace NTSW_CENOPS.Application.Access;

public interface IAccessService
{
    /// <summary>Always checks live, non-deleted database user/roles/permissions; default deny.</summary>
    Task<bool> HasAccessAsync(ClaimsPrincipal user, string key, CancellationToken cancellationToken = default);
    Task<IReadOnlySet<string>> GetCurrentAccessKeysAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AccessModel>> GetAllAsync(ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task<AdminOperationResult> CreateAsync(AccessModel model, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task<AdminOperationResult> UpdateAsync(AccessModel model, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task<AdminOperationResult> DeleteAsync(int id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task<IReadOnlySet<int>> GetAccessRoleIdsAsync(int accessId, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task<AdminOperationResult> UpdateAccessRolesAsync(int accessId, IReadOnlyCollection<int> roleIds, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task<IReadOnlySet<int>> GetRoleAccessIdsAsync(int roleId, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task<AdminOperationResult> UpdateRoleAccessesAsync(int roleId, IReadOnlyCollection<int> accessIds, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
}
