using BotFramework.Contracts.Messaging;
using Dapper;
using System.Data.Common;

namespace BotFramework.Host.Economics.Services;

/// <summary>
/// Keeps the legacy numeric wallet API compatible while separating wallets
/// belonging to the same Telegram private chat in different bot tenants.
/// Telegram uses the user's id as the private-chat id, so the numeric scope
/// alone is not a sufficient boundary when two bots share a wallet database.
/// </summary>
public sealed class WalletScopeResolver
{
    public async Task<long> ResolveAsync(
        long sourceScopeId,
        DbConnection connection,
        DbTransaction? transaction,
        CancellationToken ct)
    {
        var tenantId = RequestMetadataContext.TryGetCurrent()?.Tenant?.Value;
        if (!RequiresAlias(tenantId))
            return sourceScopeId;

        return await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            """
            INSERT INTO wallet_scope_aliases (tenant_id, source_scope_id)
            VALUES (@tenantId, @sourceScopeId)
            ON CONFLICT (tenant_id, source_scope_id)
            DO UPDATE SET tenant_id = EXCLUDED.tenant_id
            RETURNING effective_scope_id
            """,
            new { tenantId, sourceScopeId },
            transaction,
            cancellationToken: ct));
    }

    public async Task CopyUserToAliasAsync(
        long userId,
        long sourceScopeId,
        long effectiveScopeId,
        DbConnection connection,
        DbTransaction? transaction,
        CancellationToken ct)
    {
        if (sourceScopeId == effectiveScopeId)
            return;

        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO users (
                telegram_user_id,
                balance_scope_id,
                display_name,
                coins,
                version,
                created_at,
                updated_at,
                last_daily_bonus_on)
            SELECT telegram_user_id,
                   @effectiveScopeId,
                   display_name,
                   coins,
                   version,
                   created_at,
                   updated_at,
                   last_daily_bonus_on
            FROM users
            WHERE telegram_user_id = @userId
              AND balance_scope_id = @sourceScopeId
            ON CONFLICT (telegram_user_id, balance_scope_id) DO NOTHING
            """,
            new { userId, sourceScopeId, effectiveScopeId },
            transaction,
            cancellationToken: ct));
    }

    public async Task CopyScopeToAliasAsync(
        long sourceScopeId,
        long effectiveScopeId,
        DbConnection connection,
        DbTransaction? transaction,
        CancellationToken ct)
    {
        if (sourceScopeId == effectiveScopeId)
            return;

        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO users (
                telegram_user_id,
                balance_scope_id,
                display_name,
                coins,
                version,
                created_at,
                updated_at,
                last_daily_bonus_on)
            SELECT telegram_user_id,
                   @effectiveScopeId,
                   display_name,
                   coins,
                   version,
                   created_at,
                   updated_at,
                   last_daily_bonus_on
            FROM users
            WHERE balance_scope_id = @sourceScopeId
            ON CONFLICT (telegram_user_id, balance_scope_id) DO NOTHING
            """,
            new { sourceScopeId, effectiveScopeId },
            transaction,
            cancellationToken: ct));
    }

    private static bool RequiresAlias(string? tenantId) =>
        !string.IsNullOrWhiteSpace(tenantId)
        && !tenantId.StartsWith("telegram:dm:", StringComparison.Ordinal)
        && !tenantId.StartsWith("telegram:chat:", StringComparison.Ordinal)
        && !string.Equals(tenantId, "legacy:default", StringComparison.Ordinal);
}
