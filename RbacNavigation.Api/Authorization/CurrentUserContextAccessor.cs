namespace RbacNavigation.Api.Authorization;

using Microsoft.AspNetCore.Http;
using RbacNavigation.Api.Extensions;
using RbacNavigation.Api.Services;

public sealed class CurrentUserContextAccessor : ICurrentUserContextAccessor
{
    private const string ContextItemKey = "__current_user_context";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly NavigationRepository _repository;
    private readonly ILogger<CurrentUserContextAccessor> _logger;

    public CurrentUserContextAccessor(
        IHttpContextAccessor httpContextAccessor,
        NavigationRepository repository,
        ILogger<CurrentUserContextAccessor> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _repository = repository;
        _logger = logger;
    }

    public async Task<CurrentUserContext?> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is null)
        {
            throw new InvalidOperationException("No active HTTP context is available for authorization.");
        }

        if (httpContext.Items.TryGetValue(ContextItemKey, out var cached) && cached is CurrentUserContext cachedContext)
        {
            return cachedContext;
        }

        if (!httpContext.User.TryGetUserId(out var userId))
        {
            _logger.LogWarning("Failed to resolve current user context because the token is missing a subject identifier.");
            return null;
        }

        var effectiveToken = cancellationToken;
        if (!effectiveToken.CanBeCanceled)
        {
            effectiveToken = httpContext.RequestAborted;
        }

        await using var connection = await _repository.CreateOpenConnectionAsync(effectiveToken);
        var record = await _repository.GetUserRoleAsync(connection, userId, effectiveToken);
        if (record is null)
        {
            _logger.LogWarning("Failed to resolve current user context because user {UserId} was not found.", userId);
            return null;
        }

        if (httpContext.User.TryGetOrgId(out var tokenOrgId) && tokenOrgId != record.OrgId)
        {
            _logger.LogWarning(
                "User {UserId} attempted to operate within organization {RequestedOrgId} but belongs to organization {ActualOrgId}.",
                userId,
                tokenOrgId,
                record.OrgId);
        }

        var permissions = PermissionSet.FromJson(record.PermissionsJson);
        var context = new CurrentUserContext(userId, record.OrgId, record.RoleId, record.RoleName, record.PermissionsJson, permissions);
        httpContext.Items[ContextItemKey] = context;
        return context;
    }
}
