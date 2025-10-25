using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RbacNavigation.Api.Extensions;
using RbacNavigation.Api.Services;

namespace RbacNavigation.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/configs/navigation")]
public sealed class NavigationConfigsController : ControllerBase
{
    private readonly NavigationRepository _repository;
    private readonly ILogger<NavigationConfigsController> _logger;

    public NavigationConfigsController(NavigationRepository repository, ILogger<NavigationConfigsController> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetNavigationConfigAsync(CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId) || !User.TryGetOrgId(out var orgId))
        {
            _logger.LogWarning("Navigation config fetch denied due to invalid token.");
            return Unauthorized(new { error = "invalid_token" });
        }

        _logger.LogInformation("User {UserId} requested navigation configuration for organization {OrgId}.", userId, orgId);

        await using var connection = await _repository.CreateOpenConnectionAsync(cancellationToken);

        var currentUserRole = await _repository.GetUserRoleAsync(connection, userId, cancellationToken);
        if (currentUserRole is null)
        {
            _logger.LogWarning("User {UserId} not found while fetching navigation configuration for organization {OrgId}.", userId, orgId);
            return Unauthorized(new { error = "user_not_found" });
        }

        var permissions = PermissionSet.FromJson(currentUserRole.PermissionsJson);
        if (!permissions.AllowsAny(("admin", "user_mgmt", "read"), ("admin", "user_mgmt", "edit")))
        {
            _logger.LogWarning("User {UserId} attempted to fetch navigation configuration for organization {OrgId} without admin permissions.", userId, orgId);
            return Forbid();
        }

        var navMapJson = await _repository.GetNavigationMapAsync(connection, orgId, cancellationToken);
        if (navMapJson is null)
        {
            _logger.LogWarning("Navigation configuration not found for organization {OrgId} when requested by user {UserId}.", orgId, userId);
            return NotFound(new { error = "nav_not_configured" });
        }

        using var document = JsonDocument.Parse(navMapJson);
        _logger.LogInformation("Navigation configuration returned for organization {OrgId} to user {UserId}.", orgId, userId);
        return Ok(new
        {
            org_id = orgId,
            value = document.RootElement.Clone()
        });
    }

    [HttpPut]
    public async Task<IActionResult> UpsertNavigationConfigAsync([FromBody] JsonDocument payload, CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId) || !User.TryGetOrgId(out var orgId))
        {
            _logger.LogWarning("Navigation config update denied due to invalid token.");
            return Unauthorized(new { error = "invalid_token" });
        }

        if (payload.RootElement.ValueKind != JsonValueKind.Object)
        {
            _logger.LogWarning("Navigation config update rejected for organization {OrgId} by user {UserId} due to invalid payload root.", orgId, userId);
            return BadRequest(new { error = "invalid_payload" });
        }

        if (!payload.RootElement.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
        {
            _logger.LogWarning("Navigation config update rejected for organization {OrgId} by user {UserId} due to invalid items definition.", orgId, userId);
            return BadRequest(new { error = "invalid_nav_map" });
        }

        await using var connection = await _repository.CreateOpenConnectionAsync(cancellationToken);

        var currentUserRole = await _repository.GetUserRoleAsync(connection, userId, cancellationToken);
        if (currentUserRole is null)
        {
            _logger.LogWarning("User {UserId} not found while updating navigation configuration for organization {OrgId}.", userId, orgId);
            return Unauthorized(new { error = "user_not_found" });
        }

        var permissions = PermissionSet.FromJson(currentUserRole.PermissionsJson);
        if (!permissions.AllowsAny(("admin", "user_mgmt", "edit")))
        {
            _logger.LogWarning("User {UserId} attempted to update navigation configuration for organization {OrgId} without admin edit permissions.", userId, orgId);
            return Forbid();
        }

        var json = payload.RootElement.GetRawText();
        await _repository.UpsertNavigationMapAsync(connection, orgId, json, cancellationToken);

        _logger.LogInformation("Navigation configuration upserted for organization {OrgId} by user {UserId}.", orgId, userId);
        return Ok(new { org_id = orgId, status = "upserted" });
    }
}
