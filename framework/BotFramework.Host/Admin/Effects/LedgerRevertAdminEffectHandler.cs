using BotFramework.Host.Admin.Execution;
using BotFramework.Host.Contracts.Economics;
using BotFramework.Sdk.Admin.Effects;

namespace BotFramework.Host.Admin.Effects;

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
            ct);

        if (row is null)
        {
            context.SetOutput("result", new LedgerRevertResult(LedgerRevertStatus.NotFound));
            return;
        }

        var reason = $"ledger.revert#{effect.LedgerId}";
        var already = await context.QuerySingleOrDefaultAsync<bool?>(
            "SELECT EXISTS(SELECT 1 FROM economics_ledger WHERE reason = @reason)",
            new { reason },
            ct);
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
            ct);
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
            ct);
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
            ct);

        context.SetOutput("result", new LedgerRevertResult(LedgerRevertStatus.Ok, newBalance));
    }

    private sealed record LedgerRow(long UserId, long BalanceScopeId, int Delta);
    private sealed record WalletRow(int Coins, long Version);
}
