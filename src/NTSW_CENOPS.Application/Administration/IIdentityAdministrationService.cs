using System.Security.Claims;

namespace NTSW_CENOPS.Application.Administration;

public interface IIdentityAdministrationService
{
    Task<IReadOnlyList<AdminUserModel>> GetUsersAsync(CancellationToken cancellationToken = default);
    Task<AdminOperationResult> CreateUserAsync(AdminUserModel user, CancellationToken cancellationToken = default);
    Task<AdminOperationResult> UpdateUserAsync(AdminUserModel user, CancellationToken cancellationToken = default);
    Task<AdminOperationResult> DeleteUserAsync(int userId, CancellationToken cancellationToken = default);
    Task<AdminOperationResult> UpdateUserRolesAsync(int userId, IReadOnlyCollection<int> roleIds, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    Task<IReadOnlySet<int>> GetRoleUserIdsAsync(int roleId, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task<AdminOperationResult> UpdateRoleUsersAsync(int roleId, IReadOnlyCollection<int> userIds, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AdminRoleModel>> GetRolesAsync(CancellationToken cancellationToken = default);
    Task<AdminOperationResult> CreateRoleAsync(AdminRoleModel role, CancellationToken cancellationToken = default);
    Task<AdminOperationResult> UpdateRoleAsync(AdminRoleModel role, CancellationToken cancellationToken = default);
    Task<AdminOperationResult> DeleteRoleAsync(int roleId, CancellationToken cancellationToken = default);
}
