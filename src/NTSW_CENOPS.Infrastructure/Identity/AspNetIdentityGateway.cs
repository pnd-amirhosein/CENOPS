using System.Security.Claims;
using NTSW_CENOPS.Application.Common.Identity;
using NTSW_CENOPS.Infrastructure.Persistence;
using NTSW_CENOPS.Infrastructure.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace NTSW_CENOPS.Infrastructure.Identity;

public sealed class AspNetIdentityGateway : IIdentityGateway
{
    private readonly AppDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly RoleManager<ApplicationRole> _roleManager;

    public AspNetIdentityGateway(
        AppDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        RoleManager<ApplicationRole> roleManager)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _signInManager = signInManager;
        _roleManager = roleManager;
    }

    public async Task<IdentityGatewayResult> SignInAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var user = await _userManager.FindByNameAsync(username);
        if (user is null)
        {
            return IdentityGatewayResult.Failure(IdentityErrorCode.InvalidCredentials);
        }

        var signInResult = await _signInManager.PasswordSignInAsync(
            user,
            password,
            isPersistent: false,
            lockoutOnFailure: true);

        if (signInResult.Succeeded)
        {
            return IdentityGatewayResult.Success();
        }

        return signInResult.IsLockedOut
            ? IdentityGatewayResult.Failure(IdentityErrorCode.LockedOut)
            : IdentityGatewayResult.Failure(IdentityErrorCode.InvalidCredentials);
    }

    public async Task<IdentityGatewayResult> CreateUserAsync(
        string displayName,
        string username,
        string password,
        string defaultRole,
        CancellationToken cancellationToken = default)
    {
        if (await _userManager.FindByNameAsync(username) is not null)
        {
            return IdentityGatewayResult.Failure(IdentityErrorCode.DuplicateUsername);
        }

        if (await _userManager.Users
            .AsNoTracking()
            .AnyAsync(x => x.DisplayName == displayName, cancellationToken))
        {
            return IdentityGatewayResult.Failure(IdentityErrorCode.DuplicateDisplayName);
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            if (!await _roleManager.RoleExistsAsync(defaultRole))
            {
                var createRoleResult = await _roleManager.CreateAsync(new ApplicationRole
                {
                    Name = defaultRole
                });

                if (!createRoleResult.Succeeded)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return IdentityGatewayResult.Failure(IdentityErrorCode.RoleCreationFailed);
                }
                var newlyCreatedRole = await _roleManager.FindByNameAsync(defaultRole);
                if (newlyCreatedRole is not null)
                    await DefaultRoleAccessSeeder.GrantOnNewRoleAsync(_dbContext, newlyCreatedRole.Id, newlyCreatedRole.Name, cancellationToken);
            }

            var user = new ApplicationUser
            {
                UserName = username,
                DisplayName = displayName
            };

            var createUserResult = await _userManager.CreateAsync(user, password);

            if (!createUserResult.Succeeded)
            {
                await transaction.RollbackAsync(cancellationToken);
                return MapCreateUserErrors(createUserResult.Errors);
            }

            var addToRoleResult = await _userManager.AddToRoleAsync(user, defaultRole);

            if (!addToRoleResult.Succeeded)
            {
                await transaction.RollbackAsync(cancellationToken);
                return IdentityGatewayResult.Failure(IdentityErrorCode.RoleCreationFailed);
            }

            await transaction.CommitAsync(cancellationToken);
            return IdentityGatewayResult.Success();
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            _dbContext.ChangeTracker.Clear();

            if (await _userManager.FindByNameAsync(username) is not null)
            {
                return IdentityGatewayResult.Failure(IdentityErrorCode.DuplicateUsername);
            }

            if (await _userManager.Users
                .AsNoTracking()
                .AnyAsync(x => x.DisplayName == displayName, cancellationToken))
            {
                return IdentityGatewayResult.Failure(IdentityErrorCode.DuplicateDisplayName);
            }

            return IdentityGatewayResult.Failure(IdentityErrorCode.OperationFailed);
        }
    }

    public async Task<DateTime?> GetBirthDateAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default)
    {
        var id = _userManager.GetUserId(principal);
        if (!int.TryParse(id, out var userId)) return null;
        return await _userManager.Users.AsNoTracking()
            .Where(x => x.Id == userId)
            .Select(x => x.BirthDate)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IdentityGatewayResult> UpdateProfileAsync(
        ClaimsPrincipal principal,
        string displayName,
        string username,
        DateTime? birthDate,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(principal);
        if (user is null)
        {
            return IdentityGatewayResult.Failure(IdentityErrorCode.UserNotFound);
        }

        var userWithSameUsername = await _userManager.FindByNameAsync(username);
        if (userWithSameUsername is not null && userWithSameUsername.Id != user.Id)
        {
            return IdentityGatewayResult.Failure(IdentityErrorCode.DuplicateUsername);
        }

        if (await _userManager.Users
            .AsNoTracking()
            .AnyAsync(
                x => x.Id != user.Id && x.DisplayName == displayName,
                cancellationToken))
        {
            return IdentityGatewayResult.Failure(IdentityErrorCode.DuplicateDisplayName);
        }

        try
        {
            user.DisplayName = displayName;
            user.BirthDate = birthDate?.Date;

            var updateResult = !string.Equals(user.UserName, username, StringComparison.Ordinal)
                ? await _userManager.SetUserNameAsync(user, username)
                : await _userManager.UpdateAsync(user);

            if (!updateResult.Succeeded)
            {
                await _dbContext.Entry(user).ReloadAsync(cancellationToken);
                return MapProfileErrors(updateResult.Errors);
            }

            await _signInManager.RefreshSignInAsync(user);
            return IdentityGatewayResult.Success();
        }
        catch (DbUpdateException)
        {
            _dbContext.ChangeTracker.Clear();

            var duplicateUsername = await _userManager.FindByNameAsync(username);
            if (duplicateUsername is not null && duplicateUsername.Id != user.Id)
            {
                return IdentityGatewayResult.Failure(IdentityErrorCode.DuplicateUsername);
            }

            if (await _userManager.Users
                .AsNoTracking()
                .AnyAsync(
                    x => x.Id != user.Id && x.DisplayName == displayName,
                    cancellationToken))
            {
                return IdentityGatewayResult.Failure(IdentityErrorCode.DuplicateDisplayName);
            }

            return IdentityGatewayResult.Failure(IdentityErrorCode.OperationFailed);
        }
    }

    public async Task<IdentityGatewayResult> ChangePasswordAsync(
        ClaimsPrincipal principal,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var user = await _userManager.GetUserAsync(principal);
        if (user is null)
        {
            return IdentityGatewayResult.Failure(IdentityErrorCode.UserNotFound);
        }

        if (!await _userManager.CheckPasswordAsync(user, currentPassword))
        {
            return IdentityGatewayResult.Failure(IdentityErrorCode.CurrentPasswordInvalid);
        }

        var result = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);
        if (result.Succeeded)
        {
            return IdentityGatewayResult.Success();
        }

        if (result.Errors.Any(x => x.Code == "PasswordMismatch"))
        {
            return IdentityGatewayResult.Failure(IdentityErrorCode.CurrentPasswordInvalid);
        }

        var passwordErrors = result.Errors
            .Where(x => x.Code.StartsWith("Password", StringComparison.Ordinal))
            .Select(x => x.Code)
            .Distinct()
            .ToArray();

        return passwordErrors.Length > 0
            ? IdentityGatewayResult.Failure(IdentityErrorCode.WeakPassword, passwordErrors)
            : IdentityGatewayResult.Failure(IdentityErrorCode.OperationFailed);
    }

    public Task SignOutAsync() => _signInManager.SignOutAsync();

    private static IdentityGatewayResult MapCreateUserErrors(IEnumerable<IdentityError> errors)
    {
        var errorList = errors.ToArray();

        if (errorList.Any(x => x.Code == "DuplicateUserName"))
        {
            return IdentityGatewayResult.Failure(IdentityErrorCode.DuplicateUsername);
        }

        if (errorList.Any(x => x.Code == "InvalidUserName"))
        {
            return IdentityGatewayResult.Failure(IdentityErrorCode.InvalidUsername);
        }

        var passwordErrors = errorList
            .Where(x => x.Code.StartsWith("Password", StringComparison.Ordinal))
            .Select(x => x.Code)
            .Distinct()
            .ToArray();

        return passwordErrors.Length > 0
            ? IdentityGatewayResult.Failure(IdentityErrorCode.WeakPassword, passwordErrors)
            : IdentityGatewayResult.Failure(IdentityErrorCode.OperationFailed);
    }

    private static IdentityGatewayResult MapProfileErrors(IEnumerable<IdentityError> errors)
    {
        var errorCodes = errors.Select(x => x.Code).ToArray();

        if (errorCodes.Contains("DuplicateUserName", StringComparer.Ordinal))
        {
            return IdentityGatewayResult.Failure(IdentityErrorCode.DuplicateUsername);
        }

        if (errorCodes.Contains("InvalidUserName", StringComparer.Ordinal))
        {
            return IdentityGatewayResult.Failure(IdentityErrorCode.InvalidUsername);
        }

        return IdentityGatewayResult.Failure(IdentityErrorCode.OperationFailed);
    }
}
