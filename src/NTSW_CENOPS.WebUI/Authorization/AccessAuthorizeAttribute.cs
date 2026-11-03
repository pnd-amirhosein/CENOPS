using Microsoft.AspNetCore.Authorization;

namespace NTSW_CENOPS.Authorization;

/// <summary>Declarative route authorization: [AccessAuthorize(AccessKeys.Contacts)].</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class AccessAuthorizeAttribute : AuthorizeAttribute
{
    public AccessAuthorizeAttribute(string key) => Policy = AccessPolicyProvider.Prefix + key;
}
