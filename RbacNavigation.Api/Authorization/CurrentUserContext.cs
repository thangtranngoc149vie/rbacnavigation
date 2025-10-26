namespace RbacNavigation.Api.Authorization;

using RbacNavigation.Api.Services;

public sealed record CurrentUserContext(
    Guid UserId,
    Guid OrgId,
    Guid RoleId,
    string RoleName,
    string? PermissionsJson,
    PermissionSet Permissions);
