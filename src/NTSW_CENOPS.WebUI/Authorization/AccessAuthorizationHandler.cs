using NTSW_CENOPS.Application.Access;
using Microsoft.AspNetCore.Authorization;

namespace NTSW_CENOPS.Authorization;

public sealed class AccessAuthorizationHandler(IAccessService accessService)
    : AuthorizationHandler<AccessRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, AccessRequirement requirement)
    {
        if (await accessService.HasAccessAsync(context.User, requirement.Key))
            context.Succeed(requirement);
    }
}
