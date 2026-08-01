using BotFramework.Host.Contracts.Economics;
using BotFramework.Sdk.Execution;
using BotFramework.Sdk.Economics;
using Dapper;
using Microsoft.Extensions.Options;

namespace BotFramework.Host.Execution;

internal sealed class PostgresAtomicEconomics(
    IOptions<BotFrameworkOptions> options,
    WalletScopeResolver? scopeResolver = null) : IAtomicEconomics
{
    private readonly int _startingCoins = options.Value.StartingCoins;

    public async Task EnsureAsync(
        WalletIdentity wallet,
        string displayName,
        IGameExecutionSession session,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(displayName);
        ArgumentNullException.ThrowIfNull(session);
        if (displayName.Length > 64) displayName = displayName[..64];

        var effectiveScopeId = scopeResolver is null
            ? wallet.BalanceScopeId
            : await scopeResolver.ResolveAsync(wallet.BalanceScopeId, session.Connection, session.Transaction, ct);

        const string sql = """
            INSERT INTO users (telegram_user_id, balance_scope_id, display_name, coins, version, created_at, updated_at)
            SELECT @userId,
                   @effectiveScopeId,
                   @displayName,
                   COALESCE(source.coins, @startingCoins),
                   COALESCE(source.version, 0),
                   COALESCE(source.created_at, now()),
                   COALESCE(source.updated_at, now())
            FROM (SELECT 1) seed
            LEFT JOIN users source
              ON source.telegram_user_id = @userId
             AND source.balance_scope_id = @sourceScopeId
            ON CONFLICT (telegram_user_id, balance_scope_id)
            DO UPDATE SET display_name = EXCLUDED.display_name, updated_at = now()
            """;
        await session.Connection.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
                userId = wallet.UserId,
                sourceScopeId = wallet.BalanceScopeId,
                effectiveScopeId,
                displayName,
                startingCoins = _startingCoins,
            },
            session.Transaction,
            cancellationToken: ct));
    }

    public async Task<WalletSnapshot> LoadAsync(
        WalletIdentity wallet,
        IGameExecutionSession session,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(session);
        var effectiveScopeId = await ResolveScopeAsync(wallet, session, ct);
        var row = await LoadRowAsync(wallet.UserId, effectiveScopeId, session, ct);
        return new WalletSnapshot(row.Coins);
    }

    public async Task<WalletMutationResult> ApplyAsync(
        WalletIdentity wallet,
        IReadOnlyList<EconomyEffect> effects,
        IGameExecutionSession session,
        string operationId,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(effects);
        ArgumentNullException.ThrowIfNull(session);

        var effectiveScopeId = await ResolveScopeAsync(wallet, session, ct);
        var row = await LoadRowAsync(wallet.UserId, effectiveScopeId, session, ct);
        if (effects.Count == 0)
            return new WalletMutationResult(false, false, new WalletSnapshot(row.Coins));

        var batch = effects.Select(ToWalletBatchEffect).ToArray();
        var decision = WalletMutationPolicy.ApplyBatch(
            new WalletMutationState(row.Coins, row.Version),
            batch,
            allowNegative: false);
        if (decision.Rejected)
            return new WalletMutationResult(false, true, new WalletSnapshot(row.Coins));

        if (decision.Ledger.Count != 0)
        {
            const string updateSql = """
                UPDATE users
                SET coins = @balance,
                    version = @version,
                    updated_at = now()
                WHERE telegram_user_id = @userId
                  AND balance_scope_id = @balanceScopeId
                """;
            await session.Connection.ExecuteAsync(new CommandDefinition(
                updateSql,
                new
                {
                    userId = wallet.UserId,
                    balanceScopeId = effectiveScopeId,
                    balance = decision.NewBalance,
                    version = decision.NewVersion,
                },
                session.Transaction,
                cancellationToken: ct));

            const string ledgerSql = """
                INSERT INTO economics_ledger (
                    telegram_user_id,
                    balance_scope_id,
                    delta,
                    balance_after,
                    reason)
                SELECT @userId,
                       @balanceScopeId,
                       batch.delta,
                       batch.balance_after,
                       batch.reason
                FROM unnest(
                    CAST(@deltas AS integer[]),
                    CAST(@balancesAfter AS integer[]),
                    CAST(@reasons AS text[]))
                    AS batch(delta, balance_after, reason)
                """;
            await session.Connection.ExecuteAsync(new CommandDefinition(
                ledgerSql,
                new
                {
                    userId = wallet.UserId,
                    balanceScopeId = effectiveScopeId,
                    deltas = decision.Ledger.Select(mutation => mutation.Delta).ToArray(),
                    balancesAfter = decision.Ledger.Select(mutation => mutation.BalanceAfter).ToArray(),
                    reasons = decision.Ledger.Select(mutation => mutation.Reason).ToArray(),
                },
                session.Transaction,
                cancellationToken: ct));
        }

        return new WalletMutationResult(decision.Applied, false, new WalletSnapshot(decision.NewBalance));
    }

    private static WalletBatchEffect ToWalletBatchEffect(EconomyEffect effect)
    {
        if (effect.Amount is <= 0 or > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(effect), effect.Amount, "Economy effect amount is outside the supported range.");

        return new WalletBatchEffect(
            effect.Kind switch
            {
                EconomyEffectKind.Debit => WalletBatchEffectKind.Debit,
                EconomyEffectKind.Credit => WalletBatchEffectKind.Credit,
                _ => throw new ArgumentOutOfRangeException(nameof(effect), effect.Kind, "Unknown economy effect kind."),
            },
            checked((int)effect.Amount),
            effect.Reason);
    }

    private async Task<long> ResolveScopeAsync(
        WalletIdentity wallet,
        IGameExecutionSession session,
        CancellationToken ct) =>
        scopeResolver is null
            ? wallet.BalanceScopeId
            : await scopeResolver.ResolveAsync(wallet.BalanceScopeId, session.Connection, session.Transaction, ct);

    private static async Task<WalletRow> LoadRowAsync(
        long userId,
        long balanceScopeId,
        IGameExecutionSession session,
        CancellationToken ct)
    {
        const string sql = """
            SELECT coins AS Coins,
                   version AS Version
            FROM users
            WHERE telegram_user_id = @userId
              AND balance_scope_id = @balanceScopeId
            FOR UPDATE
            """;
        var row = await session.Connection.QuerySingleOrDefaultAsync<WalletRow>(new CommandDefinition(
            sql,
            new { userId, balanceScopeId },
            session.Transaction,
            cancellationToken: ct));
        return row ?? throw new InvalidOperationException(
            $"Wallet {userId}:{balanceScopeId} does not exist.");
    }

    private sealed record WalletRow(int Coins, long Version);

}
