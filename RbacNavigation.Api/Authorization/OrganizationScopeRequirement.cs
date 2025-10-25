namespace RbacNavigation.Api.Authorization;

using Microsoft.AspNetCore.Authorization;

public sealed class OrganizationScopeRequirement : IAuthorizationRequirement
{
    public static OrganizationScopeRequirement Instance { get; } = new();

    private OrganizationScopeRequirement()
    {
    }
}
