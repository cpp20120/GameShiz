using BotFramework.Host.Contracts.Economics;
using BotFramework.Host.Contracts.ResponsibleGaming;
using BotFramework.Host.Persistence.Connections;
using Dapper;

namespace BotFramework.Host.Economics.Services;

/// <summary>
/// Executes one wallet mutation batch entirely inside the Wallet database.
/// Backend never receives a Wallet connection or table access for this path.
/// </summary>
public sealed class WalletAtomicExecutionService(
    INpgsqlConnectionFactory connections,
    TimeProvider timeProvider) : IWalletAtomicExecutionService
{
    public async Task EnsureUserAsync(long userId, long balanceScopeId, string displayName, CancellationToken ct)
    {
        if (displayName.Length > 64) displayName = displayName[..64];
        await using var connection = await connections.OpenAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO users (telegram_user_id, balance_scope_id, display_name, coins)
            VALUES (@userId, @balanceScopeId, @displayName, 0)
            ON CONFLICT (telegram_user_id, balance_scope_id)
            DO UPDATE SET display_name = EXCLUDED.display_name, updated_at = now()
            """,
            new { userId, balanceScopeId, displayName },
            cancellationToken: ct));
    }

    public async Task<int> GetBalanceAsync(long userId, long balanceScopeId, CancellationToken ct)
    {
        await using var connection = await connections.OpenAsync(ct);
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT coins FROM users WHERE telegram_user_id = @userId AND balance_scope_id = @balanceScopeId",
            new { userId, balanceScopeId },
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

        var existing = await connection.QuerySingleOrDefaultAsync<int?>(new CommandDefinition(
            """
            SELECT balance_after
            FROM economics_ledger
            WHERE operation_id LIKE @prefix
            ORDER BY id DESC
            LIMIT 1
            """,
            new { prefix = operationId + ":%" },
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
            new { userId, balanceScopeId },
            transaction,
            cancellationToken: ct));
        if (wallet is null)
            throw new InvalidOperationException($"Wallet {userId}:{balanceScopeId} does not exist.");

        var stake = effects
            .Where(effect => effect.Kind == WalletBatchEffectKind.Debit
                && EconomicsService.IsProtectedWager(effect.Reason))
            .Sum(effect => (long)effect.Amount);
        if (stake > 0)
            await EnforceProtectionAsync(connection, transaction, userId, stake, timeProvider, ct);

        var balance = wallet.Coins;
        var ledger = new List<LedgerLine>(effects.Count);
        for (var index = 0; index < effects.Count; index++)
        {
            var effect = effects[index];
            if (effect.Amount <= 0 || string.IsNullOrWhiteSpace(effect.Reason))
                throw new ArgumentException("Wallet batch effects require a positive amount and reason.", nameof(effects));

            var delta = effect.Kind switch
            {
                WalletBatchEffectKind.Debit => -effect.Amount,
                WalletBatchEffectKind.Credit => effect.Amount,
                _ => throw new ArgumentOutOfRangeException(nameof(effects), effect.Kind, "Unknown wallet batch effect kind."),
            };
            var nextBalance = checked(balance + delta);
            if (nextBalance < 0)
            {
                await transaction.CommitAsync(ct);
                return new WalletBatchMutationResult(false, true, wallet.Coins);
            }

            balance = nextBalance;
            ledger.Add(new LedgerLine(delta, balance, effect.Reason, $"{operationId}:{index}"));
        }

        if (ledger.Count != 0)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                UPDATE users
                SET coins = @balance, version = version + @versionDelta, updated_at = now()
                WHERE telegram_user_id = @userId AND balance_scope_id = @balanceScopeId
                """,
                new { userId, balanceScopeId, balance, versionDelta = ledger.Count },
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
                    balanceScopeId,
                    deltas = ledger.Select(line => line.Delta).ToArray(),
                    balances = ledger.Select(line => line.BalanceAfter).ToArray(),
                    reasons = ledger.Select(line => line.Reason).ToArray(),
                    operationIds = ledger.Select(line => line.OperationId).ToArray(),
                },
                transaction,
                cancellationToken: ct));
        }

        await transaction.CommitAsync(ct);
        return new WalletBatchMutationResult(true, false, balance);
    }

    private static async Task EnforceProtectionAsync(
        Npgsql.NpgsqlConnection connection,
        Npgsql.NpgsqlTransaction transaction,
        long userId,
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
            new { userId },
            transaction,
            cancellationToken: ct));
        if (protection is null) return;

        var utcNow = timeProvider.GetUtcNow();
        if (protection.SelfExcludedUntil is { } excluded && excluded > utcNow)
            throw new PlayerProtectionException("self_excluded", excluded);
        if (protection.CooldownUntil is { } cooldown && cooldown > utcNow)
            throw new PlayerProtectionException("cooldown", cooldown);
        if (protection.DailyLimit is { } limit && protection.UsedToday + stake > limit)
            throw new PlayerProtectionException("daily_limit", dailyLimit: limit, usedToday: protection.UsedToday);
    }

    private sealed record WalletRow(int Coins, long Version);
    private sealed record LedgerLine(int Delta, int BalanceAfter, string Reason, string OperationId);
    private sealed record ProtectionRow(
        int? DailyLimit,
        DateTimeOffset? CooldownUntil,
        DateTimeOffset? SelfExcludedUntil,
        long UsedToday);
}
