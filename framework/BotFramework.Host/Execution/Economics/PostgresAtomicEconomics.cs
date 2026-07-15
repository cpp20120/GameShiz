using BotFramework.Sdk.Execution;
using Dapper;
using Microsoft.Extensions.Options;

namespace BotFramework.Host.Execution;

internal sealed class PostgresAtomicEconomics(
    IOptions<BotFrameworkOptions> options) : IAtomicEconomics
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

        const string sql = """
            INSERT INTO users (telegram_user_id, balance_scope_id, display_name, coins)
            VALUES (@userId, @balanceScopeId, @displayName, @startingCoins)
            ON CONFLICT (telegram_user_id, balance_scope_id)
            DO UPDATE SET display_name = EXCLUDED.display_name, updated_at = now()
            """;
        await session.Connection.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
                userId = wallet.UserId,
                balanceScopeId = wallet.BalanceScopeId,
                displayName,
                startingCoins = _startingCoins,
            },
            session.Transaction,
            cancellationToken: ct)).ConfigureAwait(false);
    }

    public async Task<WalletSnapshot> LoadAsync(
        WalletIdentity wallet,
        IGameExecutionSession session,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(session);
        var row = await LoadRowAsync(wallet, session, ct).ConfigureAwait(false);
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

        var row = await LoadRowAsync(wallet, session, ct).ConfigureAwait(false);
        if (effects.Count == 0)
            return new WalletMutationResult(false, false, new WalletSnapshot(row.Coins));

        var balance = row.Coins;
        var ledger = new List<LedgerMutation>(effects.Count);
        foreach (var effect in effects)
        {
            if (effect.Amount is <= 0 or > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(effects), effect.Amount, "Economy effect amount is outside the supported range.");
            if (string.IsNullOrWhiteSpace(effect.Reason))
                throw new ArgumentException("Economy effect reason is required.", nameof(effects));

            var amount = checked((int)effect.Amount);
            var delta = effect.Kind switch
            {
                EconomyEffectKind.Debit => -amount,
                EconomyEffectKind.Credit => amount,
                _ => throw new ArgumentOutOfRangeException(nameof(effects), effect.Kind, "Unknown economy effect kind."),
            };

            var nextBalance = checked(balance + delta);
            if (nextBalance < 0)
                return new WalletMutationResult(false, true, new WalletSnapshot(row.Coins));

            balance = nextBalance;
            ledger.Add(new LedgerMutation(delta, balance, effect.Reason));
        }

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
                balanceScopeId = wallet.BalanceScopeId,
                balance,
                version = checked(row.Version + effects.Count),
            },
            session.Transaction,
            cancellationToken: ct)).ConfigureAwait(false);

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
                balanceScopeId = wallet.BalanceScopeId,
                deltas = ledger.Select(mutation => mutation.Delta).ToArray(),
                balancesAfter = ledger.Select(mutation => mutation.BalanceAfter).ToArray(),
                reasons = ledger.Select(mutation => mutation.Reason).ToArray(),
            },
            session.Transaction,
            cancellationToken: ct)).ConfigureAwait(false);

        return new WalletMutationResult(true, false, new WalletSnapshot(balance));
    }

    private static async Task<WalletRow> LoadRowAsync(
        WalletIdentity wallet,
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
            new { userId = wallet.UserId, balanceScopeId = wallet.BalanceScopeId },
            session.Transaction,
            cancellationToken: ct)).ConfigureAwait(false);
        return row ?? throw new InvalidOperationException(
            $"Wallet {wallet.UserId}:{wallet.BalanceScopeId} does not exist.");
    }

    private sealed record WalletRow(int Coins, long Version);

    private sealed record LedgerMutation(int Delta, int BalanceAfter, string Reason);
}
