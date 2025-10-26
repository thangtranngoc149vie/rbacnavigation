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
    private readonly NavigationService _navigationService;
    private readonly IAuthorizationService _authorizationService;
    private readonly ICurrentUserContextAccessor _userContextAccessor;
    private readonly ILogger<NavigationController> _logger;

    public NavigationController(
        NavigationService navigationService,
        IAuthorizationService authorizationService,
        ICurrentUserContextAccessor userContextAccessor,
        ILogger<NavigationController> logger)
    {
        _navigationService = navigationService;
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

        var navigationResult = await _navigationService.GetNavigationAsync(userContext, cancellationToken);
        if (navigationResult is null)
        {
            _logger.LogWarning(
                "Navigation map not configured for organization {OrgId} when requested by user {UserId}.",
                userContext.OrgId,
                userContext.UserId);
            return NotFound(new { error = "nav_not_configured" });
        }

        var etag = navigationResult.Etag;
        if (Request.Headers.TryGetValue(HeaderNames.IfNoneMatch, out var candidates)
            && candidates.Any(value => string.Equals(value, etag, StringComparison.Ordinal)))
        {
            Response.Headers[HeaderNames.ETag] = etag;
            return StatusCode(StatusCodes.Status304NotModified);
        }

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
            items = navigationResult.Composition.Items,
            derived_permissions = navigationResult.Composition.DerivedPermissions,
            etag
        });
    }

    [HttpPost("preview")]
    public async Task<IActionResult> PreviewNavigationAsync([FromBody] NavigationPreviewRequest? request, CancellationToken cancellationToken)
    {
        request ??= new NavigationPreviewRequest();
        var userContext = await _userContextAccessor.GetCurrentAsync(cancellationToken);
        if (userContext is null)
        {
            _logger.LogWarning("Navigation preview request denied because the current user context could not be resolved.");
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
                "User {UserId} attempted to preview navigation without admin permissions.",
                userContext.UserId);
            return Forbid();
        }

        var targetOrgId = request.OrgId ?? userContext.OrgId;

        var abacTokenResult = await _authorizationService.AuthorizeAsync(
            User,
            new OrgResource(User.TryGetOrgId(out var previewTokenOrgId) ? previewTokenOrgId : targetOrgId),
            new[] { OrganizationScopeRequirement.Instance });
        if (!abacTokenResult.Succeeded)
        {
            _logger.LogWarning(
                "User {UserId} attempted to preview navigation outside of organization scope {OrgId} (token: {TokenOrgId}).",
                userContext.UserId,
                targetOrgId,
                previewTokenOrgId);
            return Forbid();
        }

        var abacTargetResult = await _authorizationService.AuthorizeAsync(
            User,
            new OrgResource(targetOrgId),
            new[] { OrganizationScopeRequirement.Instance });
        if (!abacTargetResult.Succeeded)
        {
            _logger.LogWarning(
                "User {UserId} attempted to preview navigation outside organization scope {OrgId}.",
                userContext.UserId,
                targetOrgId);
            return Forbid();
        }

        try
        {
            var preview = await _navigationService.PreviewNavigationAsync(userContext, request, cancellationToken);

            _logger.LogInformation(
                "Navigation preview generated for organization {OrgId} by user {UserId}.",
                preview.OrgId,
                userContext.UserId);

            return Ok(new
            {
                org_id = preview.OrgId,
                role = new { id = preview.RoleId, name = preview.RoleName },
                derived_permissions = preview.DerivedPermissions,
                items = preview.Items
            });
        }
        catch (NavigationPreviewException ex)
        {
            _logger.LogWarning(
                ex,
                "Navigation preview failed for user {UserId} due to {ErrorCode}.",
                userContext.UserId,
                ex.ErrorCode);
            return StatusCode(ex.StatusCode, new { error = ex.ErrorCode });
        }
    }
}
