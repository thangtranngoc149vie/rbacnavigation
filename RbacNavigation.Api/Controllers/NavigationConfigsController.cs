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

    public NavigationConfigsController(NavigationRepository repository)
    {
        _repository = repository;
    }

    [HttpGet]
    public async Task<IActionResult> GetNavigationConfigAsync(CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId) || !User.TryGetOrgId(out var orgId))
        {
            return Unauthorized(new { error = "invalid_token" });
        }

        await using var connection = await _repository.CreateOpenConnectionAsync(cancellationToken);

        var currentUserRole = await _repository.GetUserRoleAsync(connection, userId, cancellationToken);
        if (currentUserRole is null)
        {
            return Unauthorized(new { error = "user_not_found" });
        }

        var permissions = PermissionSet.FromJson(currentUserRole.PermissionsJson);
        if (!permissions.AllowsAny(("admin", "user_mgmt", "read"), ("admin", "user_mgmt", "edit")))
        {
            return Forbid();
        }

        var navMapJson = await _repository.GetNavigationMapAsync(connection, orgId, cancellationToken);
        if (navMapJson is null)
        {
            return NotFound(new { error = "nav_not_configured" });
        }

        using var document = JsonDocument.Parse(navMapJson);
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
            return Unauthorized(new { error = "invalid_token" });
        }

        if (payload.RootElement.ValueKind != JsonValueKind.Object)
        {
            return BadRequest(new { error = "invalid_payload" });
        }

        if (!payload.RootElement.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
        {
            return BadRequest(new { error = "invalid_nav_map" });
        }

        await using var connection = await _repository.CreateOpenConnectionAsync(cancellationToken);

        var currentUserRole = await _repository.GetUserRoleAsync(connection, userId, cancellationToken);
        if (currentUserRole is null)
        {
            return Unauthorized(new { error = "user_not_found" });
        }

        var permissions = PermissionSet.FromJson(currentUserRole.PermissionsJson);
        if (!permissions.AllowsAny(("admin", "user_mgmt", "edit")))
        {
            return Forbid();
        }

        var json = payload.RootElement.GetRawText();
        await _repository.UpsertNavigationMapAsync(connection, orgId, json, cancellationToken);

        return Ok(new { org_id = orgId, status = "upserted" });
    }
}
