namespace RbacNavigation.Api.Authorization;

using Microsoft.AspNetCore.Authorization;

public sealed class PermissionsRequirement : IAuthorizationRequirement
{
    public static PermissionsRequirement AnyOf(params PermissionDescriptor[] permissions)
        => new(permissions, requireAll: false);

    public static PermissionsRequirement AllOf(params PermissionDescriptor[] permissions)
        => new(permissions, requireAll: true);

    public IReadOnlyList<PermissionDescriptor> Permissions { get; }

    public bool RequireAll { get; }

    public PermissionsRequirement(IReadOnlyList<PermissionDescriptor> permissions, bool requireAll)
    {
        Permissions = permissions ?? throw new ArgumentNullException(nameof(permissions));
        RequireAll = requireAll;
    }
}
