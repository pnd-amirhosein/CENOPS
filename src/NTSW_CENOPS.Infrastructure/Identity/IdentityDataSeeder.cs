using NTSW_CENOPS.Infrastructure.Authorization;
using NTSW_CENOPS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace NTSW_CENOPS.Infrastructure.Identity;

/// <summary>
/// Optional helper for creating privileged/seed users and roles through ASP.NET Core Identity.
/// Nothing is seeded automatically.
/// </summary>
public static class IdentityDataSeeder
{
    public static async Task CreateUserAsync(
        IServiceProvider services,
        string username,
        string password,
        string displayName,
        params string[] roles)
    {
        using var scope = services.CreateScope();

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();

        foreach (var roleName in roles.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct())
        {
            if (await roleManager.RoleExistsAsync(roleName))
            {
                continue;
            }

            var roleResult = await roleManager.CreateAsync(new ApplicationRole { Name = roleName });
            if (!roleResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Could not create role '{roleName}': {FormatErrors(roleResult.Errors)}");
            }
            var createdRole = await roleManager.FindByNameAsync(roleName);
            if (createdRole is not null)
                await DefaultRoleAccessSeeder.GrantOnNewRoleAsync(
                    scope.ServiceProvider.GetRequiredService<AppDbContext>(), createdRole.Id, roleName);
        }

        var user = await userManager.FindByNameAsync(username);

        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = username,
                DisplayName = displayName
            };

            var createResult = await userManager.CreateAsync(user, password);
            if (!createResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Could not create user '{username}': {FormatErrors(createResult.Errors)}");
            }
        }
        else if (!string.Equals(user.DisplayName, displayName, StringComparison.Ordinal))
        {
            user.DisplayName = displayName;
            var updateResult = await userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Could not update user '{username}': {FormatErrors(updateResult.Errors)}");
            }
        }

        foreach (var roleName in roles.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct())
        {
            if (await userManager.IsInRoleAsync(user, roleName))
            {
                continue;
            }

            var addRoleResult = await userManager.AddToRoleAsync(user, roleName);
            if (!addRoleResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Could not add user '{username}' to role '{roleName}': {FormatErrors(addRoleResult.Errors)}");
            }
        }
    }

    private static string FormatErrors(IEnumerable<IdentityError> errors) =>
        string.Join("; ", errors.Select(x => $"{x.Code}: {x.Description}"));
}
