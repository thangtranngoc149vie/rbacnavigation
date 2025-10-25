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
        var cancellationToken = context.GetCancellationToken();
        var userContext = await _contextAccessor.GetCurrentAsync(cancellationToken);
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

        if (requirement.RequireAll)
        {
            foreach (var descriptor in requirement.Permissions)
            {
                if (!permissions.Has(descriptor.Domain, descriptor.Area, descriptor.Action))
                {
                    return;
                }
            }

            context.Succeed(requirement);
            return;
        }

        foreach (var descriptor in requirement.Permissions)
        {
            if (permissions.Has(descriptor.Domain, descriptor.Area, descriptor.Action))
            {
                context.Succeed(requirement);
                return;
            }
        }
    }
}
