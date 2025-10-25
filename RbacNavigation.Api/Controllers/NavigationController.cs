using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using RbacNavigation.Api.Extensions;
using RbacNavigation.Api.Models;
using RbacNavigation.Api.Services;

namespace RbacNavigation.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/navigation")]
public sealed class NavigationController : ControllerBase
{
    private readonly NavigationRepository _repository;
    private readonly NavigationComposer _composer;
    private readonly ILogger<NavigationController> _logger;

    public NavigationController(NavigationRepository repository, NavigationComposer composer, ILogger<NavigationController> logger)
    {
        _repository = repository;
        _composer = composer;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetNavigationAsync(CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
        {
            return Unauthorized(new { error = "invalid_token" });
        }

        await using var connection = await _repository.CreateOpenConnectionAsync(cancellationToken);

        var userRole = await _repository.GetUserRoleAsync(connection, userId, cancellationToken);
        if (userRole is null)
        {
            _logger.LogWarning("User {UserId} not found when building navigation.", userId);
            return Unauthorized(new { error = "user_not_found" });
        }

        if (User.TryGetOrgId(out var tokenOrgId) && tokenOrgId != userRole.OrgId)
        {
            return Forbid();
        }

        var navMapJson = await _repository.GetNavigationMapAsync(connection, userRole.OrgId, cancellationToken);
        if (navMapJson is null)
        {
            return NotFound(new { error = "nav_not_configured" });
        }

        var etag = _composer.ComputeEtag(userRole.PermissionsJson, navMapJson);
        if (Request.Headers.TryGetValue(HeaderNames.IfNoneMatch, out var candidates)
            && candidates.Any(value => string.Equals(value, etag, StringComparison.Ordinal)))
        {
            Response.Headers[HeaderNames.ETag] = etag;
            return StatusCode(StatusCodes.Status304NotModified);
        }

        var permissionSet = PermissionSet.FromJson(userRole.PermissionsJson);
        var composition = _composer.Compose(navMapJson, permissionSet);

        Response.Headers[HeaderNames.ETag] = etag;

        return Ok(new
        {
            org_id = userRole.OrgId,
            role = new { id = userRole.RoleId, name = userRole.RoleName },
            items = composition.Items,
            derived_permissions = composition.DerivedPermissions,
            etag
        });
    }

    [HttpGet("preview")]
    public async Task<IActionResult> PreviewNavigationAsync([FromQuery(Name = "role_id")] Guid roleId, CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
        {
            return Unauthorized(new { error = "invalid_token" });
        }

        if (!User.TryGetOrgId(out var orgId))
        {
            return Unauthorized(new { error = "invalid_token" });
        }

        await using var connection = await _repository.CreateOpenConnectionAsync(cancellationToken);

        var currentUserRole = await _repository.GetUserRoleAsync(connection, userId, cancellationToken);
        if (currentUserRole is null)
        {
            return Unauthorized(new { error = "user_not_found" });
        }

        var currentPermissions = PermissionSet.FromJson(currentUserRole.PermissionsJson);
        if (!currentPermissions.AllowsAny(("admin", "user_mgmt", "read"), ("admin", "user_mgmt", "edit")))
        {
            return Forbid();
        }

        var targetRole = await _repository.GetRoleAsync(connection, roleId, cancellationToken);
        if (targetRole is null || targetRole.OrgId != orgId)
        {
            return NotFound(new { error = "role_not_found" });
        }

        var navMapJson = await _repository.GetNavigationMapAsync(connection, orgId, cancellationToken);
        if (navMapJson is null)
        {
            return NotFound(new { error = "nav_not_configured" });
        }

        var targetPermissions = PermissionSet.FromJson(targetRole.PermissionsJson);
        var composition = _composer.Compose(navMapJson, targetPermissions);

        return Ok(new
        {
            org_id = orgId,
            role = new { id = roleId, name = targetRole.RoleName },
            items = composition.Items,
            derived_permissions = composition.DerivedPermissions
        });
    }
}
