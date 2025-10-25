namespace RbacNavigation.Api.Authorization;

using Microsoft.AspNetCore.Authorization;

public sealed class OrganizationScopeAuthorizationHandler : AuthorizationHandler<OrganizationScopeRequirement, IOrganizationResource>
{
    private readonly ICurrentUserContextAccessor _contextAccessor;

    public OrganizationScopeAuthorizationHandler(ICurrentUserContextAccessor contextAccessor)
    {
        _contextAccessor = contextAccessor;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        OrganizationScopeRequirement requirement,
        IOrganizationResource resource)
    {
        if (resource is null)
        {
            return;
        }

        var cancellationToken = context.GetCancellationToken();
        var userContext = await _contextAccessor.GetCurrentAsync(cancellationToken);
        if (userContext is null)
        {
            return;
        }

        if (resource.OrgId == userContext.OrgId)
        {
            context.Succeed(requirement);
        }
    }
}
