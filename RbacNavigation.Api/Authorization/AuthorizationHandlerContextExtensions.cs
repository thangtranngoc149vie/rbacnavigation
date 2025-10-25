namespace RbacNavigation.Api.Authorization;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Filters;

internal static class AuthorizationHandlerContextExtensions
{
    public static HttpContext? TryGetHttpContext(this AuthorizationHandlerContext context)
    {
        return context.Resource switch
        {
            HttpContext httpContext => httpContext,
            AuthorizationFilterContext filterContext => filterContext.HttpContext,
            _ => null
        };
    }

    public static CancellationToken GetCancellationToken(this AuthorizationHandlerContext context)
    {
        return context.TryGetHttpContext()?.RequestAborted ?? CancellationToken.None;
    }
}
