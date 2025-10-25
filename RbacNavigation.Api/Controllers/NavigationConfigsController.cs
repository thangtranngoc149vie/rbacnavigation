using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RbacNavigation.Api.Authorization;
using RbacNavigation.Api.Extensions;
using RbacNavigation.Api.Services;

namespace RbacNavigation.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/configs/navigation")]
public sealed class NavigationConfigsController : ControllerBase
{
    private readonly NavigationRepository _repository;
    private readonly IAuthorizationService _authorizationService;
    private readonly ICurrentUserContextAccessor _userContextAccessor;
    private readonly ILogger<NavigationConfigsController> _logger;

    public NavigationConfigsController(
        NavigationRepository repository,
        IAuthorizationService authorizationService,
        ICurrentUserContextAccessor userContextAccessor,
        ILogger<NavigationConfigsController> logger)
    {
        _repository = repository;
        _authorizationService = authorizationService;
        _userContextAccessor = userContextAccessor;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetNavigationConfigAsync(CancellationToken cancellationToken)
    {
        var userContext = await _userContextAccessor.GetCurrentAsync(cancellationToken);
        if (userContext is null)
        {
            _logger.LogWarning("Navigation config fetch denied due to invalid token.");
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
                "User {UserId} attempted to fetch navigation configuration for organization {OrgId} without admin permissions.",
                userContext.UserId,
                userContext.OrgId);
            return Forbid();
        }

        var abacResult = await _authorizationService.AuthorizeAsync(
            User,
            new OrgResource(User.TryGetOrgId(out var tokenOrgId) ? tokenOrgId : userContext.OrgId),
            new[] { OrganizationScopeRequirement.Instance });
        if (!abacResult.Succeeded)
        {
            _logger.LogWarning(
                "User {UserId} attempted to fetch navigation configuration outside organization scope {OrgId} (token: {TokenOrgId}).",
                userContext.UserId,
                userContext.OrgId,
                tokenOrgId);
            return Forbid();
        }

        _logger.LogInformation(
            "User {UserId} requested navigation configuration for organization {OrgId}.",
            userContext.UserId,
            userContext.OrgId);

        await using var connection = await _repository.CreateOpenConnectionAsync(cancellationToken);

        var navMapJson = await _repository.GetNavigationMapAsync(connection, userContext.OrgId, cancellationToken);
        if (navMapJson is null)
        {
            _logger.LogWarning(
                "Navigation configuration not found for organization {OrgId} when requested by user {UserId}.",
                userContext.OrgId,
                userContext.UserId);
            return NotFound(new { error = "nav_not_configured" });
        }

        using var document = JsonDocument.Parse(navMapJson);
        _logger.LogInformation(
            "Navigation configuration returned for organization {OrgId} to user {UserId}.",
            userContext.OrgId,
            userContext.UserId);
        return Ok(new
        {
            org_id = userContext.OrgId,
            value = document.RootElement.Clone()
        });
    }

    [HttpPut]
    public async Task<IActionResult> UpsertNavigationConfigAsync([FromBody] JsonDocument payload, CancellationToken cancellationToken)
    {
        var userContext = await _userContextAccessor.GetCurrentAsync(cancellationToken);
        if (userContext is null)
        {
            _logger.LogWarning("Navigation config update denied due to invalid token.");
            return Unauthorized(new { error = "invalid_token" });
        }

        if (payload.RootElement.ValueKind != JsonValueKind.Object)
        {
            _logger.LogWarning(
                "Navigation config update rejected for organization {OrgId} by user {UserId} due to invalid payload root.",
                userContext.OrgId,
                userContext.UserId);
            return BadRequest(new { error = "invalid_payload" });
        }

        if (!payload.RootElement.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
        {
            _logger.LogWarning(
                "Navigation config update rejected for organization {OrgId} by user {UserId} due to invalid items definition.",
                userContext.OrgId,
                userContext.UserId);
            return BadRequest(new { error = "invalid_nav_map" });
        }

        var rbacResult = await _authorizationService.AuthorizeAsync(
            User,
            resource: null,
            requirements: new[]
            {
                PermissionsRequirement.AllOf(PermissionDescriptor.From("admin", "user_mgmt", "edit"))
            });
        if (!rbacResult.Succeeded)
        {
            _logger.LogWarning(
                "User {UserId} attempted to update navigation configuration for organization {OrgId} without admin edit permissions.",
                userContext.UserId,
                userContext.OrgId);
            return Forbid();
        }

        var abacResult = await _authorizationService.AuthorizeAsync(
            User,
            new OrgResource(User.TryGetOrgId(out var tokenOrgId) ? tokenOrgId : userContext.OrgId),
            new[] { OrganizationScopeRequirement.Instance });
        if (!abacResult.Succeeded)
        {
            _logger.LogWarning(
                "User {UserId} attempted to update navigation configuration outside organization scope {OrgId} (token: {TokenOrgId}).",
                userContext.UserId,
                userContext.OrgId,
                tokenOrgId);
            return Forbid();
        }

        await using var connection = await _repository.CreateOpenConnectionAsync(cancellationToken);

        var json = payload.RootElement.GetRawText();
        await _repository.UpsertNavigationMapAsync(connection, userContext.OrgId, json, cancellationToken);

        _logger.LogInformation(
            "Navigation configuration upserted for organization {OrgId} by user {UserId}.",
            userContext.OrgId,
            userContext.UserId);
        return Ok(new { org_id = userContext.OrgId, status = "upserted" });
    }
}
