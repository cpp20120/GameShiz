using BotFramework.Contracts.Tenancy;
using BotFramework.Host.Admin.Execution;

namespace BotFramework.Host.Admin.Effects;

internal static class TenantWalletAdminSql
{
    public static Task<int> EnsureAsync(
        TenantId tenantId,
        ScopeId scopeId,
        PlayerId playerId,
        string? displayName,
        IAdminExecutionContext context,
        CancellationToken ct) =>
        context.ExecuteAsync(
            """
            INSERT INTO tenant_wallets (tenant_key, scope_key, player_id, display_name)
            SELECT t.tenant_key, s.scope_key, @playerId, COALESCE(@displayName, '')
            FROM tenants t
            JOIN tenant_scopes s ON s.tenant_key = t.tenant_key AND s.scope_id = @scopeId
            WHERE t.tenant_id = @tenantId
            ON CONFLICT (tenant_key, scope_key, player_id)
            DO UPDATE SET display_name = CASE
                WHEN @displayName IS NULL THEN tenant_wallets.display_name
                ELSE EXCLUDED.display_name
            END,
            updated_at = now()
            """,
            new
            {
                tenantId = tenantId.Value,
                scopeId = scopeId.Value,
                playerId = playerId.Value,
                displayName,
            },
            ct);

    public static Task<long?> TryExistingOperationAsync(
        TenantId tenantId,
        ScopeId scopeId,
        string? operationId,
        IAdminExecutionContext context,
        CancellationToken ct) =>
        string.IsNullOrWhiteSpace(operationId)
            ? Task.FromResult<long?>(null)
            : context.QuerySingleOrDefaultAsync<long?>(
                """
                SELECT l.balance_after
                FROM tenant_wallet_ledger l
                JOIN tenants t ON t.tenant_key = l.tenant_key
                JOIN tenant_scopes s ON s.tenant_key = l.tenant_key AND s.scope_key = l.scope_key
                WHERE t.tenant_id = @tenantId AND s.scope_id = @scopeId
                  AND l.operation_id = @operationId
                ORDER BY l.id DESC
                LIMIT 1
                """,
                new { tenantId = tenantId.Value, scopeId = scopeId.Value, operationId },
                ct);

    public static Task<WalletRow?> LockAsync(
        TenantId tenantId,
        ScopeId scopeId,
        PlayerId playerId,
        IAdminExecutionContext context,
        CancellationToken ct) =>
        context.QuerySingleOrDefaultAsync<WalletRow>(
            """
            SELECT w.coins AS Balance, w.version AS Version
            FROM tenant_wallets w
            JOIN tenants t ON t.tenant_key = w.tenant_key
            JOIN tenant_scopes s ON s.tenant_key = w.tenant_key AND s.scope_key = w.scope_key
            WHERE t.tenant_id = @tenantId AND s.scope_id = @scopeId AND w.player_id = @playerId
            FOR UPDATE
            """,
            new { tenantId = tenantId.Value, scopeId = scopeId.Value, playerId = playerId.Value },
            ct);

    public static Task<int> ApplyAsync(
        TenantId tenantId,
        ScopeId scopeId,
        PlayerId playerId,
        long balance,
        long version,
        long delta,
        string reason,
        string? operationId,
        IAdminExecutionContext context,
        CancellationToken ct) =>
        context.ExecuteAsync(
            """
            WITH target AS (
                SELECT t.tenant_key, s.scope_key
                FROM tenants t
                JOIN tenant_scopes s ON s.tenant_key = t.tenant_key AND s.scope_id = @scopeId
                WHERE t.tenant_id = @tenantId
            )
            UPDATE tenant_wallets w
            SET coins = @balance, version = @version, updated_at = now()
            FROM target
            WHERE w.tenant_key = target.tenant_key
              AND w.scope_key = target.scope_key
              AND w.player_id = @playerId;
            INSERT INTO tenant_wallet_ledger
                (tenant_key, scope_key, player_id, delta, balance_after, reason, operation_id)
            SELECT target.tenant_key, target.scope_key, @playerId, @delta, @balance, @reason, @operationId
            FROM target
            """,
            new
            {
                tenantId = tenantId.Value,
                scopeId = scopeId.Value,
                playerId = playerId.Value,
                balance,
                version,
                delta,
                reason,
                operationId,
            },
            ct);

    internal sealed record WalletRow(long Balance, long Version);
}
