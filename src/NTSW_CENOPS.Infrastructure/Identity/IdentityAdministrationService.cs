using System.Security.Claims;
using NTSW_CENOPS.Application.Administration;
using NTSW_CENOPS.Domain.Authorization;
using NTSW_CENOPS.Infrastructure.Persistence;
using NTSW_CENOPS.Infrastructure.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace NTSW_CENOPS.Infrastructure.Identity;

public sealed class IdentityAdministrationService : IIdentityAdministrationService
{
    private const string DefaultUserPassword = "123@Qwerty";

    private readonly AppDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;

    public IdentityAdministrationService(
        AppDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _roleManager = roleManager;
    }

    public async Task<IReadOnlyList<AdminUserModel>> GetUsersAsync(CancellationToken cancellationToken = default)
    {
        var users = await _userManager.Users
            .AsNoTracking()
            .OrderBy(x => x.UserName)
            .Select(x => new AdminUserModel
            {
                Id = x.Id,
                Username = x.UserName ?? string.Empty,
                DisplayName = x.DisplayName
            })
            .ToListAsync(cancellationToken);

        // Bulk N:N lookup, not a database call per user. Deleted roles are filtered by EF.
        var assignments = await (from link in _dbContext.UserRoles.AsNoTracking()
                                 join role in _dbContext.Roles.AsNoTracking() on link.RoleId equals role.Id
                                 join user in _dbContext.Users.AsNoTracking() on link.UserId equals user.Id
                                 select new { link.UserId, Name = role.Name })
            .ToListAsync(cancellationToken);
        var byUser = assignments
            .Where(x => !string.IsNullOrWhiteSpace(x.Name))
            .ToLookup(x => x.UserId, x => x.Name!);
        foreach (var user in users)
            user.Roles = byUser[user.Id].Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList();
        return users;
    }

    public async Task<AdminOperationResult> CreateUserAsync(
        AdminUserModel user,
        CancellationToken cancellationToken = default)
    {
        var username = user.Username.Trim();
        var displayName = user.DisplayName.Trim();

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(displayName))
        {
            return AdminOperationResult.Failure("نام کاربری و نام نمایشی الزامی هستند.");
        }

        var normalizedUsername = _userManager.NormalizeName(username);

        var duplicateUsername = await _dbContext.Users
            .AsNoTracking()
            .AnyAsync(x => x.NormalizedUserName == normalizedUsername, cancellationToken);

        if (duplicateUsername)
        {
            return AdminOperationResult.Failure("این نام کاربری قبلاً استفاده شده است.");
        }

        var duplicateDisplayName = await _dbContext.Users
            .AsNoTracking()
            .AnyAsync(x => x.DisplayName == displayName, cancellationToken);

        if (duplicateDisplayName)
        {
            return AdminOperationResult.Failure("این نام نمایشی قبلاً استفاده شده است.");
        }

        var applicationUser = new ApplicationUser
        {
            UserName = username,
            DisplayName = displayName,
            IsDeleted = false
        };

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            if (!await _roleManager.RoleExistsAsync(RoleNames.User))
            {
                var createDefaultRoleResult = await _roleManager.CreateAsync(new ApplicationRole
                {
                    Name = RoleNames.User,
                    IsDeleted = false
                });

                if (!createDefaultRoleResult.Succeeded)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return AdminOperationResult.Failure(FormatErrors(createDefaultRoleResult.Errors));
                }
                var newUserRole = await _roleManager.FindByNameAsync(RoleNames.User);
                if (newUserRole is not null)
                    await DefaultRoleAccessSeeder.GrantOnNewRoleAsync(_dbContext, newUserRole.Id, newUserRole.Name, cancellationToken);
            }

            var createUserResult = await _userManager.CreateAsync(applicationUser, DefaultUserPassword);
            if (!createUserResult.Succeeded)
            {
                await transaction.RollbackAsync(cancellationToken);
                return AdminOperationResult.Failure(FormatErrors(createUserResult.Errors));
            }

            var addToRoleResult = await _userManager.AddToRoleAsync(applicationUser, RoleNames.User);
            if (!addToRoleResult.Succeeded)
            {
                await transaction.RollbackAsync(cancellationToken);
                return AdminOperationResult.Failure(FormatErrors(addToRoleResult.Errors));
            }

            await transaction.CommitAsync(cancellationToken);
            return AdminOperationResult.Success();
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            _dbContext.ChangeTracker.Clear();
            return AdminOperationResult.Failure("ایجاد کاربر انجام نشد. نام کاربری یا نام نمایشی تکراری است.");
        }
    }

    public async Task<AdminOperationResult> UpdateUserAsync(
        AdminUserModel user,
        CancellationToken cancellationToken = default)
    {
        var applicationUser = await _userManager.FindByIdAsync(user.Id.ToString());
        if (applicationUser is null)
        {
            return AdminOperationResult.Failure("کاربر پیدا نشد.");
        }

        var username = user.Username.Trim();
        var displayName = user.DisplayName.Trim();

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(displayName))
        {
            return AdminOperationResult.Failure("نام کاربری و نام نمایشی الزامی هستند.");
        }

        var normalizedUsername = _userManager.NormalizeName(username);

        var duplicateUsername = await _dbContext.Users
            .AsNoTracking()
            .AnyAsync(
                x => x.Id != user.Id && x.NormalizedUserName == normalizedUsername,
                cancellationToken);

        if (duplicateUsername)
        {
            return AdminOperationResult.Failure("این نام کاربری قبلاً استفاده شده است.");
        }

        var duplicateDisplayName = await _dbContext.Users
            .AsNoTracking()
            .AnyAsync(
                x => x.Id != user.Id && x.DisplayName == displayName,
                cancellationToken);

        if (duplicateDisplayName)
        {
            return AdminOperationResult.Failure("این نام نمایشی قبلاً استفاده شده است.");
        }

        applicationUser.DisplayName = displayName;

        try
        {
            IdentityResult result;

            if (!string.Equals(applicationUser.UserName, username, StringComparison.Ordinal))
            {
                result = await _userManager.SetUserNameAsync(applicationUser, username);
            }
            else
            {
                result = await _userManager.UpdateAsync(applicationUser);
            }

            return result.Succeeded
                ? AdminOperationResult.Success()
                : AdminOperationResult.Failure(FormatErrors(result.Errors));
        }
        catch (DbUpdateException)
        {
            _dbContext.ChangeTracker.Clear();
            return AdminOperationResult.Failure("ویرایش کاربر انجام نشد. نام کاربری یا نام نمایشی تکراری است.");
        }
    }

    public async Task<AdminOperationResult> DeleteUserAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return AdminOperationResult.Failure("کاربر پیدا نشد.");
        }

        user.IsDeleted = true;
        user.LockoutEnabled = true;
        user.LockoutEnd = DateTimeOffset.MaxValue;

        var result = await _userManager.UpdateSecurityStampAsync(user);

        return result.Succeeded
            ? AdminOperationResult.Success()
            : AdminOperationResult.Failure(FormatErrors(result.Errors));
    }

    public async Task<AdminOperationResult> UpdateUserRolesAsync(
        int userId, IReadOnlyCollection<int> roleIds, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        if (actor?.Identity?.IsAuthenticated != true ||
            (!actor.IsInRole(RoleNames.Owner) && !actor.IsInRole(RoleNames.Admin)))
            return AdminOperationResult.Failure("دسترسی به مدیریت نقش‌های کاربران مجاز نیست.");
        if (roleIds is null) return AdminOperationResult.Failure("فهرست نقش‌ها معتبر نیست.");
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null) return AdminOperationResult.Failure("کاربر پیدا نشد.");

        var requested = roleIds.Distinct().ToArray();
        var roles = await _dbContext.Roles.AsNoTracking()
            .Where(x => requested.Contains(x.Id))
            .Select(x => new { x.Id, x.Name })
            .ToListAsync(cancellationToken);
        if (roles.Count != requested.Length || roles.Any(x => string.IsNullOrWhiteSpace(x.Name)))
            return AdminOperationResult.Failure("یک یا چند نقش حذف شده یا معتبر نیستند. صفحه را تازه‌سازی کنید.");

        var target = roles.Select(x => x.Name!).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var existing = (await _userManager.GetRolesAsync(user)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var toRemove = existing.Except(target, StringComparer.OrdinalIgnoreCase).ToArray();
        var toAdd = target.Except(existing, StringComparer.OrdinalIgnoreCase).ToArray();
        if (toRemove.Length == 0 && toAdd.Length == 0) return AdminOperationResult.Success();
        if (!actor.IsInRole(RoleNames.Owner) &&
            toRemove.Concat(toAdd).Any(role =>
                string.Equals(role, RoleNames.Owner, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(role, RoleNames.Admin, StringComparison.OrdinalIgnoreCase)))
            return AdminOperationResult.Failure("تغییر نقش‌های مالک یا مدیر تنها توسط مالک سامانه مجاز است.");

        // Prevent deleting the last active Owner assignment accidentally.
        if (toRemove.Contains(RoleNames.Owner, StringComparer.OrdinalIgnoreCase))
        {
            var normalizedOwnerName = _roleManager.NormalizeKey(RoleNames.Owner);
            var ownerRole = await _dbContext.Roles.AsNoTracking()
                .Where(r => r.NormalizedName == normalizedOwnerName)
                .Select(r => (int?)r.Id).SingleOrDefaultAsync(cancellationToken);
            if (ownerRole.HasValue)
            {
                var otherOwners = await (from link in _dbContext.UserRoles
                                         join u in _dbContext.Users on link.UserId equals u.Id
                                         where link.RoleId == ownerRole.Value && u.Id != userId
                                         select u.Id).AnyAsync(cancellationToken);
                if (!otherOwners)
                    return AdminOperationResult.Failure("امکان حذف آخرین نقش مالک سامانه وجود ندارد.");
            }
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            if (toRemove.Length > 0)
            {
                var removal = await _userManager.RemoveFromRolesAsync(user, toRemove);
                if (!removal.Succeeded)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    _dbContext.ChangeTracker.Clear();
                    return AdminOperationResult.Failure(FormatErrors(removal.Errors));
                }
            }
            if (toAdd.Length > 0)
            {
                var addition = await _userManager.AddToRolesAsync(user, toAdd);
                if (!addition.Succeeded)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    _dbContext.ChangeTracker.Clear();
                    return AdminOperationResult.Failure(FormatErrors(addition.Errors));
                }
            }
            var stamp = await _userManager.UpdateSecurityStampAsync(user);
            if (!stamp.Succeeded)
            {
                await transaction.RollbackAsync(cancellationToken);
                _dbContext.ChangeTracker.Clear();
                return AdminOperationResult.Failure(FormatErrors(stamp.Errors));
            }
            await transaction.CommitAsync(cancellationToken);
            return AdminOperationResult.Success();
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            _dbContext.ChangeTracker.Clear();
            return AdminOperationResult.Failure("ذخیره نقش‌های کاربر انجام نشد. دوباره تلاش کنید.");
        }
    }

    // Reverse side of AspNetUserRoles. Only active users/roles are returned.
    public async Task<IReadOnlySet<int>> GetRoleUserIdsAsync(
        int roleId, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        if (!await CanManageMembersAsync(roleId, actor, cancellationToken))
            throw new UnauthorizedAccessException("مجوز مدیریت کاربران این نقش را ندارید.");
        return (await (from link in _dbContext.UserRoles.AsNoTracking()
                       join user in _dbContext.Users.AsNoTracking() on link.UserId equals user.Id
                       where link.RoleId == roleId
                       select link.UserId).ToListAsync(cancellationToken)).ToHashSet();
    }

    public async Task<AdminOperationResult> UpdateRoleUsersAsync(
        int roleId, IReadOnlyCollection<int> userIds, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        if (!await CanManageMembersAsync(roleId, actor, cancellationToken))
            return AdminOperationResult.Failure("مجوز تغییر اعضای این نقش را ندارید.");
        if (userIds is null) return AdminOperationResult.Failure("فهرست کاربران معتبر نیست.");
        var role = await _roleManager.FindByIdAsync(roleId.ToString());
        if (role?.Name is null) return AdminOperationResult.Failure("نقش پیدا نشد.");

        var desired = userIds.ToHashSet();
        var available = await _dbContext.Users.AsNoTracking()
            .Where(x => desired.Contains(x.Id)).Select(x => x.Id).ToListAsync(cancellationToken);
        if (available.Count != desired.Count)
            return AdminOperationResult.Failure("یک یا چند کاربر معتبر نیست. فهرست کاربران را تازه‌سازی کنید.");
        var existing = await (from link in _dbContext.UserRoles.AsNoTracking()
                              join user in _dbContext.Users.AsNoTracking() on link.UserId equals user.Id
                              where link.RoleId == roleId
                              select link.UserId).ToListAsync(cancellationToken);
        var current = existing.ToHashSet();
        var toAdd = desired.Except(current).ToArray();
        var toRemove = current.Except(desired).ToArray();
        if (toAdd.Length == 0 && toRemove.Length == 0) return AdminOperationResult.Success();

        if (string.Equals(role.Name, RoleNames.Owner, StringComparison.OrdinalIgnoreCase) && desired.Count == 0)
            return AdminOperationResult.Failure("امکان حذف آخرین مالک فعال سامانه وجود ندارد.");

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            foreach (var id in toRemove.Concat(toAdd))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var user = await _userManager.FindByIdAsync(id.ToString());
                if (user is null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return AdminOperationResult.Failure("کاربر پیدا نشد. فهرست را تازه‌سازی کنید.");
                }
                var change = toRemove.Contains(id)
                    ? await _userManager.RemoveFromRoleAsync(user, role.Name)
                    : await _userManager.AddToRoleAsync(user, role.Name);
                if (!change.Succeeded)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    _dbContext.ChangeTracker.Clear();
                    return AdminOperationResult.Failure(FormatErrors(change.Errors));
                }
                var stamp = await _userManager.UpdateSecurityStampAsync(user);
                if (!stamp.Succeeded)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    _dbContext.ChangeTracker.Clear();
                    return AdminOperationResult.Failure(FormatErrors(stamp.Errors));
                }
            }
            await transaction.CommitAsync(cancellationToken);
            return AdminOperationResult.Success();
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            _dbContext.ChangeTracker.Clear();
            return AdminOperationResult.Failure("تغییر اعضای نقش انجام نشد. دوباره تلاش کنید.");
        }
    }

    private async Task<bool> CanManageMembersAsync(int roleId, ClaimsPrincipal actor, CancellationToken ct)
    {
        if (actor?.Identity?.IsAuthenticated != true ||
            !int.TryParse(actor.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId)) return false;
        // Consult live active role memberships (a revoked Owner cookie must not keep its privileges).
        var actorRoles = await (from link in _dbContext.UserRoles.AsNoTracking()
                                join user in _dbContext.Users.AsNoTracking() on link.UserId equals user.Id
                                join role in _dbContext.Roles.AsNoTracking() on link.RoleId equals role.Id
                                where link.UserId == actorId
                                select role.NormalizedName).ToListAsync(ct);
        var isOwner = actorRoles.Contains("OWNER");
        if (!isOwner && !actorRoles.Contains("ADMIN")) return false;
        var roleName = await _dbContext.Roles.AsNoTracking()
            .Where(x => x.Id == roleId).Select(x => x.Name).FirstOrDefaultAsync(ct);
        if (string.IsNullOrWhiteSpace(roleName)) return false;
        return isOwner ||
            (!string.Equals(roleName, RoleNames.Owner, StringComparison.OrdinalIgnoreCase) &&
             !string.Equals(roleName, RoleNames.Admin, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<IReadOnlyList<AdminRoleModel>> GetRolesAsync(CancellationToken cancellationToken = default)
    {
        var roles = await _roleManager.Roles.AsNoTracking().OrderBy(x => x.Name)
            .Select(x => new AdminRoleModel { Id = x.Id, Name = x.Name ?? string.Empty })
            .ToListAsync(cancellationToken);
        var links = await (from grant in _dbContext.RoleAccesses.AsNoTracking()
                           join access in _dbContext.Accesses.AsNoTracking() on grant.AccessId equals access.Id
                           join role in _dbContext.Roles.AsNoTracking() on grant.RoleId equals role.Id
                           select new { grant.RoleId, access.Name }).ToListAsync(cancellationToken);
        var lookup = links.ToLookup(x => x.RoleId, x => x.Name);
        foreach (var role in roles)
            role.Accesses = lookup[role.Id].OrderBy(x => x).ToList();
        return roles;
    }

    public async Task<AdminOperationResult> CreateRoleAsync(
        AdminRoleModel role,
        CancellationToken cancellationToken = default)
    {
        var roleName = role.Name.Trim();
        if (string.IsNullOrWhiteSpace(roleName))
        {
            return AdminOperationResult.Failure("نام نقش الزامی است.");
        }

        var normalizedName = _roleManager.NormalizeKey(roleName);
        var duplicateRole = await _dbContext.Roles
            .AsNoTracking()
            .AnyAsync(x => x.NormalizedName == normalizedName, cancellationToken);

        if (duplicateRole)
        {
            return AdminOperationResult.Failure("این نقش قبلاً وجود دارد.");
        }

        try
        {
            var result = await _roleManager.CreateAsync(new ApplicationRole
            {
                Name = roleName,
                IsDeleted = false
            });

            if (result.Succeeded)
            {
                var createdRole = await _roleManager.FindByNameAsync(roleName);
                if (createdRole is not null)
                    await DefaultRoleAccessSeeder.GrantOnNewRoleAsync(_dbContext, createdRole.Id, createdRole.Name, cancellationToken);
            }
            return result.Succeeded
                ? AdminOperationResult.Success()
                : AdminOperationResult.Failure(FormatErrors(result.Errors));
        }
        catch (DbUpdateException)
        {
            _dbContext.ChangeTracker.Clear();
            return AdminOperationResult.Failure("ایجاد نقش انجام نشد. نام نقش تکراری است.");
        }
    }

    public async Task<AdminOperationResult> UpdateRoleAsync(
        AdminRoleModel role,
        CancellationToken cancellationToken = default)
    {
        var applicationRole = await _roleManager.FindByIdAsync(role.Id.ToString());
        if (applicationRole is null)
        {
            return AdminOperationResult.Failure("نقش پیدا نشد.");
        }

        var roleName = role.Name.Trim();
        if (string.IsNullOrWhiteSpace(roleName))
        {
            return AdminOperationResult.Failure("نام نقش الزامی است.");
        }
        var reserved = new[] { RoleNames.Owner, RoleNames.Admin, RoleNames.User };
        if (!string.Equals(applicationRole.Name, roleName, StringComparison.OrdinalIgnoreCase) &&
            (reserved.Contains(applicationRole.Name, StringComparer.OrdinalIgnoreCase) ||
             reserved.Contains(roleName, StringComparer.OrdinalIgnoreCase)))
            return AdminOperationResult.Failure("تغییر نام نقش‌های پایه (Owner/Admin/User) مجاز نیست.");

        var normalizedName = _roleManager.NormalizeKey(roleName);
        var duplicateRole = await _dbContext.Roles
            .AsNoTracking()
            .AnyAsync(
                x => x.Id != role.Id && x.NormalizedName == normalizedName,
                cancellationToken);

        if (duplicateRole)
        {
            return AdminOperationResult.Failure("این نقش قبلاً وجود دارد.");
        }

        applicationRole.Name = roleName;

        try
        {
            var result = await _roleManager.UpdateAsync(applicationRole);
            return result.Succeeded
                ? AdminOperationResult.Success()
                : AdminOperationResult.Failure(FormatErrors(result.Errors));
        }
        catch (DbUpdateException)
        {
            _dbContext.ChangeTracker.Clear();
            return AdminOperationResult.Failure("ویرایش نقش انجام نشد. نام نقش تکراری است.");
        }
    }

    public async Task<AdminOperationResult> DeleteRoleAsync(
        int roleId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var role = await _roleManager.FindByIdAsync(roleId.ToString());
        if (role is null)
        {
            return AdminOperationResult.Failure("نقش پیدا نشد.");
        }
        if (new[] { RoleNames.Owner, RoleNames.Admin, RoleNames.User }
            .Contains(role.Name, StringComparer.OrdinalIgnoreCase))
            return AdminOperationResult.Failure("حذف نقش‌های پایه Owner، Admin و User مجاز نیست.");


        var isAssignedToAnyUser = await _dbContext.UserRoles
            .AsNoTracking()
            .AnyAsync(x => x.RoleId == roleId, cancellationToken);

        if (isAssignedToAnyUser)
        {
            return AdminOperationResult.Failure(
                $"امکان حذف نقش «{role.Name}» وجود ندارد؛ این نقش به یک یا چند کاربر اختصاص داده شده است. ابتدا ارتباط کاربران با این نقش را حذف کنید.");
        }

        role.IsDeleted = true;
        var result = await _roleManager.UpdateAsync(role);

        return result.Succeeded
            ? AdminOperationResult.Success()
            : AdminOperationResult.Failure(FormatErrors(result.Errors));
    }

    private static string FormatErrors(IEnumerable<IdentityError> errors)
    {
        var messages = errors
            .Select(x => x.Description)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .ToArray();

        return messages.Length == 0
            ? "عملیات انجام نشد."
            : string.Join(" | ", messages);
    }
}
