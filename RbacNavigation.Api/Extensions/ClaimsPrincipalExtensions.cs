using System.Security.Claims;

namespace RbacNavigation.Api.Extensions;

public static class ClaimsPrincipalExtensions
{
    private const string OrgIdClaim = "org_id";
    private const string SubClaim = "sub";

    public static bool TryGetUserId(this ClaimsPrincipal principal, out Guid userId)
    {
        var raw = principal.FindFirstValue(SubClaim) ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(raw, out userId);
    }

    public static bool TryGetOrgId(this ClaimsPrincipal principal, out Guid orgId)
    {
        var raw = principal.FindFirstValue(OrgIdClaim);
        return Guid.TryParse(raw, out orgId);
    }

    public static string? GetPermissionsJson(this ClaimsPrincipal principal)
    {
        return principal.FindFirstValue("permissions") ?? principal.FindFirstValue("role_permissions");
    }
}
