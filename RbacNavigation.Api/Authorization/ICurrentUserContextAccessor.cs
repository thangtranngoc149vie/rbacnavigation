namespace RbacNavigation.Api.Authorization;

public interface ICurrentUserContextAccessor
{
    Task<CurrentUserContext?> GetCurrentAsync(CancellationToken cancellationToken = default);
}
