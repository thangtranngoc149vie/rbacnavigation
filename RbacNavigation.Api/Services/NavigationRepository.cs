using System.Data;
using System.Data.Common;
using Dapper;
using RbacNavigation.Api.Data;
using RbacNavigation.Api.Models;

namespace RbacNavigation.Api.Services;

public sealed class NavigationRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    private const string UserRoleSql = """
SELECT u.org_id AS OrgId,
       r.id AS RoleId,
       r.name AS RoleName,
       r.permissions::text AS PermissionsJson
FROM users u
JOIN roles r ON r.id = u.role_id
WHERE u.id = @userId
LIMIT 1;
""";

    private const string NavMapSql = """
SELECT value::text
FROM configs
WHERE org_id = @orgId AND key = 'nav_map_v1'
LIMIT 1;
""";

    private const string RoleSql = """
SELECT id AS RoleId,
       name AS RoleName,
       permissions::text AS PermissionsJson,
       org_id AS OrgId
FROM roles
WHERE id = @roleId
LIMIT 1;
""";

    private const string UpsertConfigSql = """
INSERT INTO configs (id, org_id, key, value, created_at)
VALUES (uuid_generate_v4(), @orgId, 'nav_map_v1', CAST(@value AS jsonb), now())
ON CONFLICT (org_id, key)
DO UPDATE SET value = CAST(EXCLUDED.value AS jsonb), updated_at = now();
""";

    public NavigationRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public ValueTask<DbConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken)
    {
        return _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
    }

    public Task<UserRoleRecord?> GetUserRoleAsync(DbConnection connection, Guid userId, CancellationToken cancellationToken)
    {
        var command = CreateCommand(UserRoleSql, parameters =>
        {
            parameters.Add("userId", userId, DbType.Guid);
        }, cancellationToken);

        return connection.QuerySingleOrDefaultAsync<UserRoleRecord>(command);
    }

    public Task<string?> GetNavigationMapAsync(DbConnection connection, Guid orgId, CancellationToken cancellationToken)
    {
        var command = CreateCommand(NavMapSql, parameters =>
        {
            parameters.Add("orgId", orgId, DbType.Guid);
        }, cancellationToken);

        return connection.QuerySingleOrDefaultAsync<string>(command);
    }

    public Task<RoleRecord?> GetRoleAsync(DbConnection connection, Guid roleId, CancellationToken cancellationToken)
    {
        var command = CreateCommand(RoleSql, parameters =>
        {
            parameters.Add("roleId", roleId, DbType.Guid);
        }, cancellationToken);

        return connection.QuerySingleOrDefaultAsync<RoleRecord>(command);
    }

    public Task UpsertNavigationMapAsync(DbConnection connection, Guid orgId, string value, CancellationToken cancellationToken)
    {
        var command = CreateCommand(UpsertConfigSql, parameters =>
        {
            parameters.Add("orgId", orgId, DbType.Guid);
            parameters.Add("value", value, DbType.String);
        }, cancellationToken);

        return connection.ExecuteAsync(command);
    }

    private static CommandDefinition CreateCommand(string sql, Action<DynamicParameters> configureParameters, CancellationToken cancellationToken)
    {
        var parameters = new DynamicParameters();
        configureParameters(parameters);
        return new CommandDefinition(sql, parameters, cancellationToken: cancellationToken);
    }
}
