using BotFramework.Contracts.Economics;
using BotFramework.Contracts.Tenancy;
using BotFramework.Host.Persistence.Connections;
using Dapper;

namespace BotFramework.Host.Economics;

/// <summary>
/// Reads the canonical SDK wallet table. Legacy demo modules continue to use
/// the numeric wallet service until their deliberate migration.
/// </summary>
public sealed class PostgresTenantWalletReadService(INpgsqlConnectionFactory connections)
    : ITenantWalletReadService
{
    public async Task<TenantWalletAccount?> GetAsync(
        TenantContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.PlayerId is not { } player)
            return null;

        await using var connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var row = await connection.QuerySingleOrDefaultAsync<WalletRow>(new CommandDefinition(
            """
            SELECT w.coins AS Balance,
                   w.version AS Version,
                   w.created_at AS CreatedAt,
                   w.updated_at AS UpdatedAt
            FROM tenant_wallets w
            JOIN tenants t ON t.tenant_key = w.tenant_key
            JOIN tenant_scopes s ON s.tenant_key = w.tenant_key AND s.scope_key = w.scope_key
            WHERE t.tenant_id = @tenantId
              AND s.scope_id = @scopeId
              AND w.player_id = @playerId
            """,
            new
            {
                tenantId = context.TenantId.Value,
                scopeId = context.ScopeId.Value,
                playerId = player.Value,
            },
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        return row is null
            ? null
            : new TenantWalletAccount(
                context.TenantId,
                context.ScopeId,
                player,
                row.Balance,
                row.Version,
                row.CreatedAt,
                row.UpdatedAt);
    }

    private sealed class WalletRow
    {
        public long Balance { get; init; }
        public long Version { get; init; }
        public DateTimeOffset CreatedAt { get; init; }
        public DateTimeOffset UpdatedAt { get; init; }
    }
}
