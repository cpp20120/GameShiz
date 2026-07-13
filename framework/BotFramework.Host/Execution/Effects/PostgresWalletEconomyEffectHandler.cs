using BotFramework.Sdk.Execution;

namespace BotFramework.Host.Execution;

internal sealed class PostgresWalletEconomyEffectHandler : GameEffectHandler<WalletEconomyEffect>
{
    protected override async Task ApplyBatchAsync(
        IReadOnlyList<WalletEconomyEffect> effects,
        IGameExecutionContext context,
        CancellationToken ct)
    {
        foreach (var group in effects.GroupBy(effect => (effect.UserId, effect.BalanceScopeId)))
        {
            foreach (var effect in group)
            {
                if (effect.Amount > int.MaxValue)
                    throw new ArgumentOutOfRangeException(nameof(effects), effect.Amount, "Wallet effect amount is too large.");
                var delta = effect.Kind switch
                {
                    EconomyEffectKind.Credit => checked((int)effect.Amount),
                    EconomyEffectKind.Debit => -checked((int)effect.Amount),
                    _ => throw new ArgumentOutOfRangeException(nameof(effects), effect.Kind, "Unknown wallet effect kind."),
                };
                var balance = await context.QuerySingleOrDefaultAsync<int?>("""
                    UPDATE users
                    SET coins=coins+@delta,version=version+1,updated_at=now()
                    WHERE telegram_user_id=@UserId AND balance_scope_id=@BalanceScopeId
                      AND coins+@delta>=0
                    RETURNING coins
                    """, new { effect.UserId, effect.BalanceScopeId, delta }, ct)
                    ?? throw new InvalidOperationException(
                        $"Wallet {effect.UserId}:{effect.BalanceScopeId} is missing or rejected the mutation.");
                await context.ExecuteAsync("""
                    INSERT INTO economics_ledger
                        (telegram_user_id,balance_scope_id,delta,balance_after,reason)
                    VALUES (@UserId,@BalanceScopeId,@delta,@balance,@Reason)
                    """, new { effect.UserId, effect.BalanceScopeId, delta, balance, effect.Reason }, ct);
            }
        }
    }
}
