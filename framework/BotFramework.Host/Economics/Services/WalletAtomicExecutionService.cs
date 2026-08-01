using BotFramework.Host.Contracts.Economics;
using BotFramework.Host.Contracts.ResponsibleGaming;
using BotFramework.Host.Economics;
using BotFramework.Host.Persistence.Connections;
using BotFramework.Sdk.Economics;
using Dapper;

namespace BotFramework.Host.Economics.Services;

/// <summary>
/// Executes one wallet mutation batch entirely inside the Wallet database.
/// Backend never receives a Wallet connection or table access for this path.
/// </summary>
public sealed class WalletAtomicExecutionService(
    INpgsqlConnectionFactory connections,
    TimeProvider timeProvider,
    WalletScopeResolver? scopeResolver = null) : IWalletAtomicExecutionService
{
    public async Task EnsureUserAsync(long userId, long balanceScopeId, string displayName, CancellationToken ct)
    {
        if (displayName.Length > 64) displayName = displayName[..64];
        await using var connection = await connections.OpenAsync(ct);
        var effectiveScopeId = scopeResolver is null
            ? balanceScopeId
            : await scopeResolver.ResolveAsync(balanceScopeId, connection, null, ct);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO users (telegram_user_id, balance_scope_id, display_name, coins, version, created_at, updated_at)
            SELECT @userId,
                   @effectiveScopeId,
                   @displayName,
                   COALESCE(source.coins, 0),
                   COALESCE(source.version, 0),
                   COALESCE(source.created_at, now()),
                   COALESCE(source.updated_at, now())
            FROM (SELECT 1) seed
            LEFT JOIN users source
              ON source.telegram_user_id = @userId
             AND source.balance_scope_id = @sourceScopeId
            ON CONFLICT (telegram_user_id, balance_scope_id)
            DO UPDATE SET display_name = EXCLUDED.display_name, updated_at = now()
            """,
            new { userId, sourceScopeId = balanceScopeId, effectiveScopeId, displayName },
            cancellationToken: ct));
    }

    public async Task<int> GetBalanceAsync(long userId, long balanceScopeId, CancellationToken ct)
    {
        await using var connection = await connections.OpenAsync(ct);
        var effectiveScopeId = scopeResolver is null
            ? balanceScopeId
            : await scopeResolver.ResolveAsync(balanceScopeId, connection, null, ct);
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT coins FROM users WHERE telegram_user_id = @userId AND balance_scope_id = @balanceScopeId",
            new { userId, balanceScopeId = effectiveScopeId },
            cancellationToken: ct));
    }

    public async Task<WalletBatchMutationResult> ApplyBatchAsync(
        long userId,
        long balanceScopeId,
        IReadOnlyList<WalletBatchEffect> effects,
        string operationId,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(effects);
        if (string.IsNullOrWhiteSpace(operationId) || operationId.Length > 240)
            throw new ArgumentException("A bounded operation id is required.", nameof(operationId));

        await using var connection = await connections.OpenAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        var effectiveScopeId = scopeResolver is null
            ? balanceScopeId
            : await scopeResolver.ResolveAsync(balanceScopeId, connection, transaction, ct);

        var existing = await connection.QuerySingleOrDefaultAsync<int?>(new CommandDefinition(
            """
            SELECT balance_after
            FROM economics_ledger
            WHERE operation_id LIKE @prefix
              AND balance_scope_id = @balanceScopeId
            ORDER BY id DESC
            LIMIT 1
            """,
            new { prefix = operationId + ":%", balanceScopeId = effectiveScopeId },
            transaction,
            cancellationToken: ct));
        if (existing is not null)
        {
            await transaction.CommitAsync(ct);
            return new WalletBatchMutationResult(true, false, existing.Value);
        }

        var wallet = await connection.QuerySingleOrDefaultAsync<WalletRow>(new CommandDefinition(
            """
            SELECT coins AS Coins, version AS Version
            FROM users
            WHERE telegram_user_id = @userId
              AND balance_scope_id = @balanceScopeId
            FOR UPDATE
            """,
            new { userId, balanceScopeId = effectiveScopeId },
            transaction,
            cancellationToken: ct));
        if (wallet is null)
            throw new InvalidOperationException($"Wallet {userId}:{balanceScopeId} does not exist.");

        var stake = effects
            .Where(effect => effect.Kind == WalletBatchEffectKind.Debit
                && WalletMutationPolicy.IsProtectedWager(effect.Reason))
            .Sum(effect => (long)effect.Amount);
        if (stake > 0)
            await EnforceProtectionAsync(connection, transaction, userId, effectiveScopeId, stake, timeProvider, ct);

        var decision = WalletMutationPolicy.ApplyBatch(
            new WalletMutationState(wallet.Coins, wallet.Version),
            effects,
            allowNegative: false);
        if (decision.Rejected)
        {
            await transaction.CommitAsync(ct);
            return new WalletBatchMutationResult(false, true, wallet.Coins);
        }

        var ledger = decision.Ledger
            .Select((line, index) => new LedgerLine(line, $"{operationId}:{index}"))
            .ToArray();
        if (ledger.Length != 0)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                UPDATE users
                SET coins = @balance, version = version + @versionDelta, updated_at = now()
                WHERE telegram_user_id = @userId AND balance_scope_id = @balanceScopeId
                """,
                new { userId, balanceScopeId = effectiveScopeId, balance = decision.NewBalance, versionDelta = ledger.Length },
                transaction,
                cancellationToken: ct));

            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO economics_ledger
                    (telegram_user_id, balance_scope_id, delta, balance_after, reason, operation_id)
                SELECT @userId, @balanceScopeId, batch.delta, batch.balance_after, batch.reason, batch.operation_id
                FROM unnest(
                    CAST(@deltas AS integer[]),
                    CAST(@balances AS integer[]),
                    CAST(@reasons AS text[]),
                    CAST(@operationIds AS text[]))
                    AS batch(delta, balance_after, reason, operation_id)
                """,
                new
                {
                    userId,
                    balanceScopeId = effectiveScopeId,
                    deltas = ledger.Select(line => line.Mutation.Delta).ToArray(),
                    balances = ledger.Select(line => line.Mutation.BalanceAfter).ToArray(),
                    reasons = ledger.Select(line => line.Mutation.Reason).ToArray(),
                    operationIds = ledger.Select(line => line.OperationId).ToArray(),
                },
                transaction,
                cancellationToken: ct));
        }

        await transaction.CommitAsync(ct);
        return new WalletBatchMutationResult(decision.Applied, false, decision.NewBalance);
    }

    private static async Task EnforceProtectionAsync(
        Npgsql.NpgsqlConnection connection,
        Npgsql.NpgsqlTransaction transaction,
        long userId,
        long balanceScopeId,
        long stake,
        TimeProvider timeProvider,
        CancellationToken ct)
    {
        const string sql = """
            SELECT p.daily_stake_limit AS DailyLimit,
                   p.cooldown_until AS CooldownUntil,
                   p.self_excluded_until AS SelfExcludedUntil,
                   COALESCE((
                       SELECT sum(-l.delta)
                       FROM economics_ledger l
                       WHERE l.telegram_user_id = @userId
                         AND l.balance_scope_id = @balanceScopeId
                         AND l.delta < 0
                         AND l.created_at >= date_trunc('day', now() AT TIME ZONE 'UTC') AT TIME ZONE 'UTC'
                         AND l.reason NOT LIKE 'admin.%'
                         AND l.reason NOT LIKE 'transfer.%'
                         AND l.reason NOT LIKE '%.rollback'
                   ), 0)::bigint AS UsedToday
            FROM player_protection p
            WHERE p.telegram_user_id = @userId
            """;
        var protection = await connection.QuerySingleOrDefaultAsync<ProtectionRow>(new CommandDefinition(
            sql,
            new { userId, balanceScopeId },
            transaction,
            cancellationToken: ct));
        if (protection is null) return;

        PlayerProtectionGuard.EnsureAllowed(PlayerProtectionPolicy.Evaluate(
            new PlayerProtectionState(
                protection.DailyLimit,
                protection.CooldownUntil,
                protection.SelfExcludedUntil,
                protection.UsedToday),
            stake,
            timeProvider.GetUtcNow()));
    }

    private sealed record WalletRow(int Coins, long Version);
    private sealed record LedgerLine(WalletMutationLine Mutation, string OperationId);
    private sealed record ProtectionRow(
        int? DailyLimit,
        DateTimeOffset? CooldownUntil,
        DateTimeOffset? SelfExcludedUntil,
        long UsedToday);
}
