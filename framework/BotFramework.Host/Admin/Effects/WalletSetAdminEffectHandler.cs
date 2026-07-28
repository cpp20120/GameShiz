using BotFramework.Host.Admin.Execution;
using BotFramework.Host.Contracts.Economics;
using BotFramework.Host.Economics.Services;
using BotFramework.Sdk.Admin.Effects;
using Microsoft.Extensions.Options;

namespace BotFramework.Host.Admin.Effects;

internal sealed class WalletSetAdminEffectHandler(
    IOptions<BotFrameworkOptions> options) : AdminEffectHandler<WalletSetAdminEffect>
{
    private readonly int _startingCoins = options.Value.StartingCoins;

    protected override async Task ApplyAsync(
        WalletSetAdminEffect effect,
        IAdminExecutionContext context,
        CancellationToken ct)
    {
        if (effect.OperationId is not null)
        {
            var existing = await context.QuerySingleOrDefaultAsync<int?>(
                "SELECT balance_after FROM economics_ledger WHERE operation_id = @operationId",
                new { operationId = effect.OperationId }, ct);
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
                new { userId = effect.UserId, balanceScopeId = effect.BalanceScopeId, displayName, startingCoins = _startingCoins },
                ct);
        }

        var row = await context.QuerySingleOrDefaultAsync<WalletRow>(
            """
            SELECT coins AS Coins, version AS Version
            FROM users
            WHERE telegram_user_id = @userId AND balance_scope_id = @balanceScopeId
            FOR UPDATE
            """,
            new { userId = effect.UserId, balanceScopeId = effect.BalanceScopeId }, ct);
        if (row is null)
            throw new InvalidOperationException($"Wallet {effect.UserId}:{effect.BalanceScopeId} does not exist.");

        if (effect.OperationId is not null)
        {
            var existing = await context.QuerySingleOrDefaultAsync<int?>(
                "SELECT balance_after FROM economics_ledger WHERE operation_id = @operationId",
                new { operationId = effect.OperationId }, ct);
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
                }, ct);
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
                }, ct);
        }

        context.SetOutput("balance", effect.Balance);
    }

    private sealed record WalletRow(int Coins, long Version);
}
