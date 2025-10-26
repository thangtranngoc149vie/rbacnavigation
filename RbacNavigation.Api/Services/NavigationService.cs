using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using RbacNavigation.Api.Authorization;
using RbacNavigation.Api.Models;

namespace RbacNavigation.Api.Services;

public sealed class NavigationService
{
    private readonly NavigationRepository _repository;
    private readonly NavigationComposer _composer;
    private readonly NavigationContentSanitizer _sanitizer;

    public NavigationService(
        NavigationRepository repository,
        NavigationComposer composer,
        NavigationContentSanitizer sanitizer)
    {
        _repository = repository;
        _composer = composer;
        _sanitizer = sanitizer;
    }

    public async Task<NavigationFetchResult?> GetNavigationAsync(CurrentUserContext userContext, CancellationToken cancellationToken)
    {
        await using var connection = await _repository.CreateOpenConnectionAsync(cancellationToken);

        var navMapJson = await _repository.GetNavigationMapAsync(connection, userContext.OrgId, cancellationToken);
        if (navMapJson is null)
        {
            return null;
        }

        var sanitizedNavMapJson = _sanitizer.Sanitize(navMapJson);
        var composition = _composer.Compose(sanitizedNavMapJson, userContext.Permissions);
        var etag = _composer.ComputeEtag(userContext.PermissionsJson, sanitizedNavMapJson);

        return new NavigationFetchResult(
            userContext.OrgId,
            userContext.RoleId,
            userContext.RoleName,
            composition,
            etag);
    }

    public async Task<NavigationPreviewResult> PreviewNavigationAsync(
        CurrentUserContext currentUser,
        NavigationPreviewRequest request,
        CancellationToken cancellationToken)
    {
        await using var connection = await _repository.CreateOpenConnectionAsync(cancellationToken);

        var targetOrgId = request.OrgId ?? currentUser.OrgId;

        var navMapJson = await ResolveNavigationJsonAsync(connection, targetOrgId, request, cancellationToken);
        var permissionContext = await ResolvePermissionContextAsync(connection, targetOrgId, currentUser, request, cancellationToken);

        NavMapDocument navMap;
        try
        {
            navMap = _composer.Deserialize(navMapJson);
        }
        catch (JsonException)
        {
            throw NavigationPreviewException.InvalidNavigation();
        }
        catch (InvalidOperationException)
        {
            throw NavigationPreviewException.InvalidNavigation();
        }

        var includeHidden = request.IncludeHidden ?? true;
        var returnReason = request.ReturnReason ?? true;

        var items = BuildPreviewItems(navMap, permissionContext.PermissionSet, includeHidden, returnReason);

        return new NavigationPreviewResult(
            permissionContext.OrgId,
            permissionContext.RoleId,
            permissionContext.RoleName,
            permissionContext.PermissionSet.Flatten(),
            items);
    }

    private async Task<string> ResolveNavigationJsonAsync(
        DbConnection connection,
        Guid targetOrgId,
        NavigationPreviewRequest request,
        CancellationToken cancellationToken)
    {
        if (request.HasDraftNavValue)
        {
            if (request.DraftNavValue.ValueKind != JsonValueKind.Object)
            {
                throw NavigationPreviewException.InvalidNavigation();
            }

            return _sanitizer.Sanitize(request.DraftNavValue);
        }

        var stored = await _repository.GetNavigationMapAsync(connection, targetOrgId, cancellationToken);
        if (stored is null)
        {
            throw NavigationPreviewException.NavigationNotConfigured(targetOrgId);
        }

        return _sanitizer.Sanitize(stored);
    }

    private async Task<PreviewPermissionContext> ResolvePermissionContextAsync(
        DbConnection connection,
        Guid requestedOrgId,
        CurrentUserContext currentUser,
        NavigationPreviewRequest request,
        CancellationToken cancellationToken)
    {
        var resolvedOrgId = requestedOrgId;
        var resolvedRoleId = currentUser.RoleId;
        var resolvedRoleName = currentUser.RoleName;
        var resolvedPermissions = currentUser.Permissions;

        if (request.AsUserId.HasValue)
        {
            var userRecord = await _repository.GetUserRoleAsync(connection, request.AsUserId.Value, cancellationToken);
            if (userRecord is null)
            {
                throw NavigationPreviewException.UserNotFound(request.AsUserId.Value);
            }

            if (userRecord.OrgId != requestedOrgId)
            {
                throw NavigationPreviewException.OrganizationMismatch(requestedOrgId, userRecord.OrgId);
            }

            resolvedOrgId = userRecord.OrgId;
            resolvedRoleId = userRecord.RoleId;
            resolvedRoleName = userRecord.RoleName;
            resolvedPermissions = PermissionSet.FromJson(userRecord.PermissionsJson);
        }

        if (request.AsRoleId.HasValue)
        {
            var roleRecord = await _repository.GetRoleAsync(connection, request.AsRoleId.Value, cancellationToken);
            if (roleRecord is null)
            {
                throw NavigationPreviewException.RoleNotFound(request.AsRoleId.Value);
            }

            if (roleRecord.OrgId != requestedOrgId)
            {
                throw NavigationPreviewException.OrganizationMismatch(requestedOrgId, roleRecord.OrgId);
            }

            resolvedOrgId = roleRecord.OrgId;
            resolvedRoleId = roleRecord.RoleId;
            resolvedRoleName = roleRecord.RoleName;
            resolvedPermissions = PermissionSet.FromJson(roleRecord.PermissionsJson);
        }

        if (request.AsPermissions is { Count: > 0 })
        {
            var normalizedPermissions = request.AsPermissions
                .Select(static scope => scope?.Trim())
                .Where(static scope => !string.IsNullOrWhiteSpace(scope))
                .Select(static scope => scope!);

            resolvedPermissions = PermissionSet.FromFlatScopes(normalizedPermissions);
        }

        return new PreviewPermissionContext(resolvedOrgId, resolvedRoleId, resolvedRoleName, resolvedPermissions);
    }

    private static IReadOnlyList<NavigationPreviewItemDto> BuildPreviewItems(
        NavMapDocument navMap,
        PermissionSet permissionSet,
        bool includeHidden,
        bool returnReason)
    {
        var items = new List<NavigationPreviewItemDto>();
        foreach (var item in navMap.Items)
        {
            var requires = item.Requires ?? new List<NavigationScopeRequirement>();
            var isVisible = permissionSet.Allows(requires, out var matchedScope);
            if (!includeHidden && !isVisible)
            {
                continue;
            }

            var requiresScopes = requires.Count == 0
                ? Array.Empty<string>()
                : requires.Select(static requirement => requirement.Scope).ToArray();

            string? reason = null;
            string? matched = null;
            if (returnReason)
            {
                if (requiresScopes.Length == 0)
                {
                    reason = "public";
                }
                else if (isVisible)
                {
                    matched = matchedScope;
                    reason = matchedScope is null ? "matched" : $"matched: {matchedScope}";
                }
                else
                {
                    reason = $"missing: {requiresScopes[0]}";
                }
            }

            items.Add(new NavigationPreviewItemDto(
                item.Key,
                item.Label,
                item.Route,
                item.Icon,
                isVisible,
                requiresScopes,
                reason,
                matched));
        }

        return items;
    }

    private sealed record PreviewPermissionContext(Guid OrgId, Guid RoleId, string RoleName, PermissionSet PermissionSet);
}

public sealed record NavigationFetchResult(
    Guid OrgId,
    Guid RoleId,
    string RoleName,
    NavigationComposition Composition,
    string Etag);

public sealed record NavigationPreviewResult(
    Guid OrgId,
    Guid RoleId,
    string RoleName,
    IReadOnlyDictionary<string, string[]> DerivedPermissions,
    IReadOnlyList<NavigationPreviewItemDto> Items);
