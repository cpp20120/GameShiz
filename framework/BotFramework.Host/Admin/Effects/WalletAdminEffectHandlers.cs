using BotFramework.Host.Admin.Execution;
using BotFramework.Host.Contracts.Economics;
using BotFramework.Host.Economics.Services;
using BotFramework.Sdk.Admin.Effects;
using Dapper;
using Microsoft.Extensions.Options;

namespace BotFramework.Host.Admin.Effects;

internal sealed class WalletAdjustmentAdminEffectHandler(
    IOptions<BotFrameworkOptions> options) : AdminEffectHandler<WalletAdjustmentAdminEffect>
{
    private readonly int startingCoins = options.Value.StartingCoins;

    protected override async Task ApplyAsync(
        WalletAdjustmentAdminEffect effect,
        IAdminExecutionContext context,
        CancellationToken ct)
    {
        if (effect.Delta == 0 && effect.DisplayName is null)
        {
            var current = await ReadBalanceAsync(effect.UserId, effect.BalanceScopeId, context, ct).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"Wallet {effect.UserId}:{effect.BalanceScopeId} does not exist.");
            context.SetOutput("balance", current);
            return;
        }

        if (effect.DisplayName is not null)
        {
            var displayName = effect.DisplayName.Length > 64 ? effect.DisplayName[..64] : effect.DisplayName;
            await context.ExecuteAsync(
                """
                INSERT INTO users (telegram_user_id, balance_scope_id, display_name, coins)
                VALUES (@userId, @balanceScopeId, @displayName, @startingCoins)
                ON CONFLICT (telegram_user_id, balance_scope_id)
                DO UPDATE SET display_name = EXCLUDED.display_name, updated_at = now()
                """,
                new { userId = effect.UserId, balanceScopeId = effect.BalanceScopeId, displayName, startingCoins },
                ct).ConfigureAwait(false);
        }

        if (effect.OperationId is not null)
        {
            var existing = await context.QuerySingleOrDefaultAsync<int?>(
                "SELECT balance_after FROM economics_ledger WHERE operation_id = @operationId",
                new { operationId = effect.OperationId },
                ct).ConfigureAwait(false);
            if (existing.HasValue)
            {
                context.SetOutput("balance", existing.Value);
                return;
            }
        }

        var row = await context.QuerySingleOrDefaultAsync<WalletRow>(
            """
            SELECT coins AS Coins, version AS Version
            FROM users
            WHERE telegram_user_id = @userId AND balance_scope_id = @balanceScopeId
            FOR UPDATE
            """,
            new { userId = effect.UserId, balanceScopeId = effect.BalanceScopeId },
            ct).ConfigureAwait(false);

        if (row is null)
            throw new InvalidOperationException($"Wallet {effect.UserId}:{effect.BalanceScopeId} does not exist.");

        // A concurrent retry can observe the operation as absent before waiting
        // for the wallet lock. Re-check after FOR UPDATE so the ledger insert is
        // idempotent even when both requests started at the same time.
        if (effect.OperationId is not null)
        {
            var existing = await context.QuerySingleOrDefaultAsync<int?>(
                "SELECT balance_after FROM economics_ledger WHERE operation_id = @operationId",
                new { operationId = effect.OperationId },
                ct).ConfigureAwait(false);
            if (existing.HasValue)
            {
                context.SetOutput("balance", existing.Value);
                return;
            }
        }

        var newBalance = checked(row.Coins + effect.Delta);
        if (!effect.AllowNegative && newBalance < 0)
            throw new InsufficientFundsException(effect.UserId, effect.BalanceScopeId, -effect.Delta, row.Coins);

        if (effect.Delta != 0)
        {
            await context.ExecuteAsync(
                """
                UPDATE users
                SET coins = @newBalance, version = @newVersion, updated_at = now()
                WHERE telegram_user_id = @userId AND balance_scope_id = @balanceScopeId
                """,
                new
                {
                    userId = effect.UserId,
                    balanceScopeId = effect.BalanceScopeId,
                    newBalance,
                    newVersion = checked(row.Version + 1),
                },
                ct).ConfigureAwait(false);

            await context.ExecuteAsync(
                """
                INSERT INTO economics_ledger
                    (telegram_user_id, balance_scope_id, delta, balance_after, reason, operation_id)
                VALUES (@userId, @balanceScopeId, @delta, @newBalance, @reason, @operationId)
                """,
                new
                {
                    userId = effect.UserId,
                    balanceScopeId = effect.BalanceScopeId,
                    delta = effect.Delta,
                    newBalance,
                    reason = effect.Reason,
                    operationId = effect.OperationId,
                },
                ct).ConfigureAwait(false);
        }

        context.SetOutput("balance", newBalance);
    }

    private static Task<int?> ReadBalanceAsync(long userId, long balanceScopeId, IAdminExecutionContext context, CancellationToken ct) =>
        context.QuerySingleOrDefaultAsync<int?>(
            "SELECT coins FROM users WHERE telegram_user_id = @userId AND balance_scope_id = @balanceScopeId",
            new { userId, balanceScopeId },
            ct);

    private sealed record WalletRow(int Coins, long Version);
}

internal sealed class WalletSetAdminEffectHandler(
    IOptions<BotFrameworkOptions> options) : AdminEffectHandler<WalletSetAdminEffect>
{
    private readonly int startingCoins = options.Value.StartingCoins;

    protected override async Task ApplyAsync(
        WalletSetAdminEffect effect,
        IAdminExecutionContext context,
        CancellationToken ct)
    {
        if (effect.OperationId is not null)
        {
            var existing = await context.QuerySingleOrDefaultAsync<int?>(
                "SELECT balance_after FROM economics_ledger WHERE operation_id = @operationId",
                new { operationId = effect.OperationId }, ct).ConfigureAwait(false);
            if (existing.HasValue)
            {
                context.SetOutput("balance", existing.Value);
                return;
            }
        }

        if (effect.DisplayName is not null)
        {
            var displayName = effect.DisplayName.Length > 64 ? effect.DisplayName[..64] : effect.DisplayName;
            await context.ExecuteAsync(
                """
                INSERT INTO users (telegram_user_id, balance_scope_id, display_name, coins)
                VALUES (@userId, @balanceScopeId, @displayName, @startingCoins)
                ON CONFLICT (telegram_user_id, balance_scope_id)
                DO UPDATE SET display_name = EXCLUDED.display_name, updated_at = now()
                """,
                new { userId = effect.UserId, balanceScopeId = effect.BalanceScopeId, displayName, startingCoins },
                ct).ConfigureAwait(false);
        }

        var row = await context.QuerySingleOrDefaultAsync<WalletRow>(
            """
            SELECT coins AS Coins, version AS Version
            FROM users
            WHERE telegram_user_id = @userId AND balance_scope_id = @balanceScopeId
            FOR UPDATE
            """,
            new { userId = effect.UserId, balanceScopeId = effect.BalanceScopeId }, ct).ConfigureAwait(false);
        if (row is null)
            throw new InvalidOperationException($"Wallet {effect.UserId}:{effect.BalanceScopeId} does not exist.");

        if (effect.OperationId is not null)
        {
            var existing = await context.QuerySingleOrDefaultAsync<int?>(
                "SELECT balance_after FROM economics_ledger WHERE operation_id = @operationId",
                new { operationId = effect.OperationId }, ct).ConfigureAwait(false);
            if (existing.HasValue)
            {
                context.SetOutput("balance", existing.Value);
                return;
            }
        }

        if (!effect.AllowNegative && effect.Balance < 0)
            throw new InsufficientFundsException(effect.UserId, effect.BalanceScopeId, -effect.Balance, row.Coins);

        var delta = checked(effect.Balance - row.Coins);
        if (delta != 0)
        {
            await context.ExecuteAsync(
                """
                UPDATE users
                SET coins = @balance, version = @version, updated_at = now()
                WHERE telegram_user_id = @userId AND balance_scope_id = @balanceScopeId
                """,
                new
                {
                    userId = effect.UserId,
                    balanceScopeId = effect.BalanceScopeId,
                    balance = effect.Balance,
                    version = checked(row.Version + 1),
                }, ct).ConfigureAwait(false);
            await context.ExecuteAsync(
                """
                INSERT INTO economics_ledger
                    (telegram_user_id, balance_scope_id, delta, balance_after, reason, operation_id)
                VALUES (@userId, @balanceScopeId, @delta, @balance, @reason, @operationId)
                """,
                new
                {
                    userId = effect.UserId,
                    balanceScopeId = effect.BalanceScopeId,
                    delta,
                    balance = effect.Balance,
                    reason = effect.Reason,
                    operationId = effect.OperationId,
                }, ct).ConfigureAwait(false);
        }

        context.SetOutput("balance", effect.Balance);
    }

    private sealed record WalletRow(int Coins, long Version);
}

internal sealed class LedgerRevertAdminEffectHandler : AdminEffectHandler<LedgerRevertAdminEffect>
{
    protected override async Task ApplyAsync(
        LedgerRevertAdminEffect effect,
        IAdminExecutionContext context,
        CancellationToken ct)
    {
        var row = await context.QuerySingleOrDefaultAsync<LedgerRow>(
            """
            SELECT telegram_user_id AS UserId,
                   balance_scope_id AS BalanceScopeId,
                   delta AS Delta
            FROM economics_ledger
            WHERE id = @ledgerId
            FOR UPDATE
            """,
            new { ledgerId = effect.LedgerId },
            ct).ConfigureAwait(false);

        if (row is null)
        {
            context.SetOutput("result", new LedgerRevertResult(LedgerRevertStatus.NotFound));
            return;
        }

        var reason = $"ledger.revert#{effect.LedgerId}";
        var already = await context.QuerySingleOrDefaultAsync<bool?>(
            "SELECT EXISTS(SELECT 1 FROM economics_ledger WHERE reason = @reason)",
            new { reason },
            ct).ConfigureAwait(false);
        if (already == true)
        {
            context.SetOutput("result", new LedgerRevertResult(LedgerRevertStatus.AlreadyReverted));
            return;
        }

        var correctionLong = -(long)row.Delta;
        if (correctionLong is > int.MaxValue or < int.MinValue)
        {
            context.SetOutput("result", new LedgerRevertResult(LedgerRevertStatus.CorrectionOutOfRange));
            return;
        }

        var correction = (int)correctionLong;
        var wallet = await context.QuerySingleOrDefaultAsync<WalletRow>(
            """
            SELECT coins AS Coins, version AS Version
            FROM users
            WHERE telegram_user_id = @userId AND balance_scope_id = @balanceScopeId
            FOR UPDATE
            """,
            new { userId = row.UserId, balanceScopeId = row.BalanceScopeId },
            ct).ConfigureAwait(false);
        if (wallet is null)
        {
            context.SetOutput("result", new LedgerRevertResult(LedgerRevertStatus.UserMissing));
            return;
        }

        if (correction == 0)
        {
            context.SetOutput("result", new LedgerRevertResult(LedgerRevertStatus.NoEffect, wallet.Coins));
            return;
        }

        var newBalance = checked(wallet.Coins + correction);
        await context.ExecuteAsync(
            """
            UPDATE users
            SET coins = @newBalance, version = @newVersion, updated_at = now()
            WHERE telegram_user_id = @userId AND balance_scope_id = @balanceScopeId
            """,
            new
            {
                userId = row.UserId,
                balanceScopeId = row.BalanceScopeId,
                newBalance,
                newVersion = checked(wallet.Version + 1),
            },
            ct).ConfigureAwait(false);
        await context.ExecuteAsync(
            """
            INSERT INTO economics_ledger
                (telegram_user_id, balance_scope_id, delta, balance_after, reason)
            VALUES (@userId, @balanceScopeId, @delta, @newBalance, @reason)
            """,
            new
            {
                userId = row.UserId,
                balanceScopeId = row.BalanceScopeId,
                delta = correction,
                newBalance,
                reason,
            },
            ct).ConfigureAwait(false);

        context.SetOutput("result", new LedgerRevertResult(LedgerRevertStatus.Ok, newBalance));
    }

    private sealed record LedgerRow(long UserId, long BalanceScopeId, int Delta);
    private sealed record WalletRow(int Coins, long Version);
}
