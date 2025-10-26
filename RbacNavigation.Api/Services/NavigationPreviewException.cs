using System;
using Microsoft.AspNetCore.Http;

namespace RbacNavigation.Api.Services;

public sealed class NavigationPreviewException : Exception
{
    public NavigationPreviewException(int statusCode, string errorCode, string message)
        : base(message)
    {
        StatusCode = statusCode;
        ErrorCode = errorCode;
    }

    public int StatusCode { get; }

    public string ErrorCode { get; }

    public static NavigationPreviewException NavigationNotConfigured(Guid orgId)
    {
        return new NavigationPreviewException(
            StatusCodes.Status404NotFound,
            "nav_not_configured",
            $"Navigation map is not configured for organization {orgId}.");
    }

    public static NavigationPreviewException RoleNotFound(Guid roleId)
    {
        return new NavigationPreviewException(
            StatusCodes.Status404NotFound,
            "role_not_found",
            $"Role {roleId} was not found.");
    }

    public static NavigationPreviewException UserNotFound(Guid userId)
    {
        return new NavigationPreviewException(
            StatusCodes.Status404NotFound,
            "user_not_found",
            $"User {userId} was not found.");
    }

    public static NavigationPreviewException OrganizationMismatch(Guid requestedOrgId, Guid actualOrgId)
    {
        return new NavigationPreviewException(
            StatusCodes.Status403Forbidden,
            "org_mismatch",
            $"Requested organization {requestedOrgId} does not match resource organization {actualOrgId}.");
    }

    public static NavigationPreviewException InvalidNavigation()
    {
        return new NavigationPreviewException(
            StatusCodes.Status400BadRequest,
            "invalid_nav_map",
            "Navigation map is invalid or malformed.");
    }
}
