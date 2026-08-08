using BotFramework.Contracts.Messaging;
using BotFramework.Contracts.Tenancy;
using Dapper;
using Npgsql;

namespace BotFramework.Host.Persistence.Connections;

/// <summary>
/// Projects the ambient framework tenant into every PostgreSQL connection.
///
/// Game modules deliberately do not depend on this type. The database boundary
/// owns the session variables so a later schema migration can add RLS or
/// tenant-keyed tables without changing game commands and stores.
/// </summary>
internal static class TenantDatabaseScope
{
    public static NpgsqlConnection CreateConnection(string connectionString) => new(connectionString);

    public static async Task ApplyAsync(NpgsqlConnection connection, CancellationToken ct)
    {
        var values = CurrentValues();
        await connection.ExecuteAsync(new CommandDefinition(
            """
            SELECT set_config('casinoshiz.tenant_id', @tenantId, false),
                   set_config('casinoshiz.scope_id', @scopeId, false),
                   set_config('casinoshiz.player_id', @playerId, false),
                   set_config('casinoshiz.channel', @channel, false),
                   set_config('casinoshiz.tenant_bound', @bound, false)
            """,
            values,
            cancellationToken: ct));
    }

    private static object CurrentValues()
    {
        var tenant = RequestMetadataContext.TryGetCurrent()?.TenantContext;
        return new
        {
            tenantId = tenant?.TenantId.Value ?? string.Empty,
            scopeId = tenant?.ScopeId.Value ?? string.Empty,
            playerId = tenant?.PlayerId?.Value ?? string.Empty,
            channel = tenant?.Channel.ToString().ToLowerInvariant() ?? string.Empty,
            bound = tenant is null ? "false" : "true",
        };
    }

}
