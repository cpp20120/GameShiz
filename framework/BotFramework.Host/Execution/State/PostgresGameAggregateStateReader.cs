using BotFramework.Contracts.Messaging;
using BotFramework.Host.Persistence.Connections;
using Dapper;

namespace BotFramework.Host.Execution;

public sealed class PostgresGameAggregateStateReader(
    INpgsqlConnectionFactory connections) : IGameAggregateStateReader
{
    public async Task<string?> LoadJsonAsync(
        string gameId,
        string aggregateId,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameId);
        ArgumentException.ThrowIfNullOrWhiteSpace(aggregateId);

        await using var connection = await connections.OpenAsync(ct);
        var tenant = RequestMetadataContext.TryGetCurrent()?.TenantContext;
        if (tenant is null)
        {
            return await connection.QuerySingleOrDefaultAsync<string>(new CommandDefinition(
                """
                SELECT state::text
                FROM game_aggregate_states
                WHERE game_id = @gameId AND aggregate_id = @aggregateId
                """,
                new { gameId, aggregateId },
                cancellationToken: ct));
        }

        return await connection.QuerySingleOrDefaultAsync<string>(new CommandDefinition(
            """
            SELECT a.state::text
            FROM tenant_aggregate_states a
            JOIN tenants t ON t.tenant_key = a.tenant_key
            JOIN tenant_scopes s ON s.tenant_key = a.tenant_key AND s.scope_key = a.scope_key
            WHERE t.tenant_id = @tenantId
              AND s.scope_id = @scopeId
              AND a.game_id = @gameId
              AND a.aggregate_id = @aggregateId
            """,
            new
            {
                tenantId = tenant.TenantId.Value,
                scopeId = tenant.ScopeId.Value,
                gameId,
                aggregateId,
            },
            cancellationToken: ct));
    }
}
