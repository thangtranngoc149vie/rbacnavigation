using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using RbacNavigation.Api.Authorization;
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
    private readonly IAuthorizationService _authorizationService;
    private readonly ICurrentUserContextAccessor _userContextAccessor;
    private readonly ILogger<NavigationController> _logger;

    public NavigationController(
        NavigationRepository repository,
        NavigationComposer composer,
        IAuthorizationService authorizationService,
        ICurrentUserContextAccessor userContextAccessor,
        ILogger<NavigationController> logger)
    {
        _repository = repository;
        _composer = composer;
        _authorizationService = authorizationService;
        _userContextAccessor = userContextAccessor;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetNavigationAsync(CancellationToken cancellationToken)
    {
        var userContext = await _userContextAccessor.GetCurrentAsync(cancellationToken);
        if (userContext is null)
        {
            _logger.LogWarning("Navigation request denied because the current user context could not be resolved.");
            return Unauthorized(new { error = "invalid_token" });
        }

        var abacResult = await _authorizationService.AuthorizeAsync(
            User,
            new OrgResource(User.TryGetOrgId(out var tokenOrgId) ? tokenOrgId : userContext.OrgId),
            new[] { OrganizationScopeRequirement.Instance });
        if (!abacResult.Succeeded)
        {
            _logger.LogWarning(
                "User {UserId} attempted to access navigation outside of organization scope {OrgId} (token: {TokenOrgId}).",
                userContext.UserId,
                userContext.OrgId,
                tokenOrgId);
            return Forbid();
        }

        _logger.LogInformation("User {UserId} requested navigation menu for organization {OrgId}.", userContext.UserId, userContext.OrgId);

        await using var connection = await _repository.CreateOpenConnectionAsync(cancellationToken);

        var navMapJson = await _repository.GetNavigationMapAsync(connection, userContext.OrgId, cancellationToken);
        if (navMapJson is null)
        {
            _logger.LogWarning(
                "Navigation map not configured for organization {OrgId} when requested by user {UserId}.",
                userContext.OrgId,
                userContext.UserId);
            return NotFound(new { error = "nav_not_configured" });
        }

        var etag = _composer.ComputeEtag(userContext.PermissionsJson, navMapJson);
        if (Request.Headers.TryGetValue(HeaderNames.IfNoneMatch, out var candidates)
            && candidates.Any(value => string.Equals(value, etag, StringComparison.Ordinal)))
        {
            Response.Headers[HeaderNames.ETag] = etag;
            return StatusCode(StatusCodes.Status304NotModified);
        }

        var composition = _composer.Compose(navMapJson, userContext.Permissions);

        Response.Headers[HeaderNames.ETag] = etag;

        _logger.LogInformation(
            "Navigation generated for user {UserId} in organization {OrgId} with role {RoleId}.",
            userContext.UserId,
            userContext.OrgId,
            userContext.RoleId);

        return Ok(new
        {
            org_id = userContext.OrgId,
            role = new { id = userContext.RoleId, name = userContext.RoleName },
            items = composition.Items,
            derived_permissions = composition.DerivedPermissions,
            etag
        });
    }

    [HttpGet("preview")]
    public async Task<IActionResult> PreviewNavigationAsync([FromQuery(Name = "role_id")] Guid roleId, CancellationToken cancellationToken)
    {
        var userContext = await _userContextAccessor.GetCurrentAsync(cancellationToken);
        if (userContext is null)
        {
            _logger.LogWarning("Navigation preview request denied because the current user context could not be resolved for role {RoleId}.", roleId);
            return Unauthorized(new { error = "invalid_token" });
        }

        var rbacResult = await _authorizationService.AuthorizeAsync(
            User,
            resource: null,
            requirements: new[]
            {
                PermissionsRequirement.AnyOf(
                    PermissionDescriptor.From("admin", "user_mgmt", "read"),
                    PermissionDescriptor.From("admin", "user_mgmt", "edit"))
            });
        if (!rbacResult.Succeeded)
        {
            _logger.LogWarning(
                "User {UserId} attempted to preview navigation for role {RoleId} without admin permissions.",
                userContext.UserId,
                roleId);
            return Forbid();
        }

        var abacTokenResult = await _authorizationService.AuthorizeAsync(
            User,
            new OrgResource(User.TryGetOrgId(out var previewTokenOrgId) ? previewTokenOrgId : userContext.OrgId),
            new[] { OrganizationScopeRequirement.Instance });
        if (!abacTokenResult.Succeeded)
        {
            _logger.LogWarning(
                "User {UserId} attempted to preview navigation for role {RoleId} outside of organization scope {OrgId} (token: {TokenOrgId}).",
                userContext.UserId,
                roleId,
                userContext.OrgId,
                previewTokenOrgId);
            return Forbid();
        }

        await using var connection = await _repository.CreateOpenConnectionAsync(cancellationToken);

        var targetRole = await _repository.GetRoleAsync(connection, roleId, cancellationToken);
        if (targetRole is null)
        {
            _logger.LogWarning(
                "Navigation preview requested for missing role {RoleId} by user {UserId}.",
                roleId,
                userContext.UserId);
            return NotFound(new { error = "role_not_found" });
        }

        var abacResult = await _authorizationService.AuthorizeAsync(
            User,
            new OrgResource(targetRole.OrgId),
            new[] { OrganizationScopeRequirement.Instance });
        if (!abacResult.Succeeded)
        {
            _logger.LogWarning(
                "User {UserId} attempted to preview navigation for role {RoleId} outside organization scope {OrgId}.",
                userContext.UserId,
                roleId,
                targetRole.OrgId);
            return Forbid();
        }

        var navMapJson = await _repository.GetNavigationMapAsync(connection, targetRole.OrgId, cancellationToken);
        if (navMapJson is null)
        {
            _logger.LogWarning(
                "Navigation map not configured for organization {OrgId} when preview requested by user {UserId}.",
                targetRole.OrgId,
                userContext.UserId);
            return NotFound(new { error = "nav_not_configured" });
        }

        var targetPermissions = PermissionSet.FromJson(targetRole.PermissionsJson);
        var composition = _composer.Compose(navMapJson, targetPermissions);

        _logger.LogInformation(
            "Navigation preview generated for role {RoleId} in organization {OrgId} by user {UserId}.",
            roleId,
            targetRole.OrgId,
            userContext.UserId);

        return Ok(new
        {
            org_id = targetRole.OrgId,
            role = new { id = roleId, name = targetRole.RoleName },
            items = composition.Items,
            derived_permissions = composition.DerivedPermissions
        });
    }
}
