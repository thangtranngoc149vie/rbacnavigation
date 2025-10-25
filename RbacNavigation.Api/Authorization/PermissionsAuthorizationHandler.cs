namespace RbacNavigation.Api.Authorization;

using Microsoft.AspNetCore.Authorization;

public sealed class PermissionsAuthorizationHandler : AuthorizationHandler<PermissionsRequirement>
{
    private readonly ICurrentUserContextAccessor _contextAccessor;

    public PermissionsAuthorizationHandler(ICurrentUserContextAccessor contextAccessor)
    {
        _contextAccessor = contextAccessor;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionsRequirement requirement)
    {
        var userContext = await _contextAccessor.GetCurrentAsync();
        if (userContext is null)
        {
            return;
        }

        if (requirement.Permissions.Count == 0)
        {
            context.Succeed(requirement);
            return;
        }

        var permissions = userContext.Permissions;
        var matches = 0;

        foreach (var descriptor in requirement.Permissions)
        {
            if (permissions.Has(descriptor.Domain, descriptor.Area, descriptor.Action))
            {
                if (!requirement.RequireAll)
                {
                    context.Succeed(requirement);
                    return;
                }

                matches++;
            }
            else if (requirement.RequireAll)
            {
                return;
            }
        }

        if (requirement.RequireAll && matches == requirement.Permissions.Count)
        {
            context.Succeed(requirement);
        }
        else if (!requirement.RequireAll && requirement.Permissions.Count == 0)
        {
            context.Succeed(requirement);
        }
    }
}
