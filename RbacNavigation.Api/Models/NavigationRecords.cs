namespace RbacNavigation.Api.Models;

public sealed record UserRoleRecord(Guid OrgId, Guid RoleId, string RoleName, string? PermissionsJson);

public sealed record RoleRecord(Guid RoleId, string RoleName, string? PermissionsJson, Guid OrgId);
