using BotFramework.Host.Execution;
using Games.Pick.Application.Execution;

namespace Games.Pick.Infrastructure.Persistence;

public sealed class PickWalletCreditEffectHandler : GameEffectHandler<PickWalletCreditEffect>
{
    protected override async Task ApplyBatchAsync(IReadOnlyList<PickWalletCreditEffect> effects, IGameExecutionContext context, CancellationToken ct)
    {
        foreach (var e in effects)
        {
            if (e.Amount <= 0) continue;
            var balance = await context.QuerySingleOrDefaultAsync<int?>("""
                UPDATE users SET coins=coins+@Amount,version=version+1,updated_at=now()
                WHERE telegram_user_id=@UserId AND balance_scope_id=@ChatId RETURNING coins
                """, e, ct) ?? throw new InvalidOperationException($"Wallet {e.UserId}:{e.ChatId} not found.");
            await context.ExecuteAsync("INSERT INTO economics_ledger (telegram_user_id,balance_scope_id,delta,balance_after,reason) VALUES (@UserId,@ChatId,@Amount,@balance,@Reason)", new { e.UserId, e.ChatId, e.Amount, balance, e.Reason }, ct);
        }
    }
}
