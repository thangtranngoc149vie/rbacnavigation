using System.Data.Common;

namespace RbacNavigation.Api.Data;

public interface IDbConnectionFactory
{
    ValueTask<DbConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken = default);
}
