using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace NTSW_CENOPS.Authorization;

/// <summary>Supports any access key entered in the database without registering one policy per key.</summary>
public sealed class AccessPolicyProvider(IOptions<AuthorizationOptions> options) : IAuthorizationPolicyProvider
{
    public const string Prefix = "access:";
    private readonly DefaultAuthorizationPolicyProvider _fallback = new(options);

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => _fallback.GetDefaultPolicyAsync();
    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => _fallback.GetFallbackPolicyAsync();
    public Task<AuthorizationPolicy?> GetPolicyAsync(string name)
    {
        if (!name.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
            return _fallback.GetPolicyAsync(name);
        var key = name[Prefix.Length..];
        if (string.IsNullOrWhiteSpace(key)) return Task.FromResult<AuthorizationPolicy?>(null);
        return Task.FromResult<AuthorizationPolicy?>(new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .AddRequirements(new AccessRequirement(key))
            .Build());
    }
}

public sealed record AccessRequirement(string Key) : IAuthorizationRequirement;
