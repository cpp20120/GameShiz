using System.Globalization;
using System.Text.Json;
using BotFramework.Contracts.Tenancy;
using Dapper;
using Games.Blackjack.Application.Execution;

namespace Games.Blackjack.Infrastructure.Persistence;

public sealed class BlackjackStateReader(
    INpgsqlConnectionFactory connections,
    ITenantContextAccessor tenantContext) : IBlackjackStateReader
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<BlackjackGameState?> LoadAsync(long userId, CancellationToken ct)
    {
        await using var connection = await connections.OpenAsync(ct);
        var aggregateId = userId.ToString(CultureInfo.InvariantCulture);
        var tenant = tenantContext.Current;
        var json = tenant is null
            ? await connection.QuerySingleOrDefaultAsync<string>(new CommandDefinition(
                """
                SELECT state::text
                FROM game_aggregate_states
                WHERE game_id = 'blackjack' AND aggregate_id = @aggregateId
                """,
                new { aggregateId },
                cancellationToken: ct))
            : await connection.QuerySingleOrDefaultAsync<string>(new CommandDefinition(
                """
                SELECT a.state::text
                FROM tenant_aggregate_states a
                JOIN tenants t ON t.tenant_key = a.tenant_key
                JOIN tenant_scopes s ON s.tenant_key = a.tenant_key AND s.scope_key = a.scope_key
                WHERE t.tenant_id = @tenantId
                  AND s.scope_id = @scopeId
                  AND a.game_id = 'blackjack'
                  AND a.aggregate_id = @aggregateId
                """,
                new
                {
                    tenantId = tenant.TenantId.Value,
                    scopeId = tenant.ScopeId.Value,
                    aggregateId,
                },
                cancellationToken: ct));
        return json is null
            ? null
            : JsonSerializer.Deserialize<BlackjackGameState>(json, JsonOptions)
                ?? throw new InvalidOperationException("Stored blackjack state is null.");
    }
}
