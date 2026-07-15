using BotFramework.Host.Execution;
using BotFramework.Sdk.Execution;
using Games.Pick.Application.Execution;

namespace Games.Pick.Infrastructure.Persistence;

public sealed class PickWalletCreditEffectHandler : GameEffectHandler<PickWalletCreditEffect>
{
    protected override async Task ApplyBatchAsync(IReadOnlyList<PickWalletCreditEffect> effects, IGameExecutionContext context, CancellationToken ct)
    {
        foreach (var group in effects.GroupBy(effect => (effect.UserId, effect.ChatId)))
        {
            var walletEffects = group
                .Where(effect => effect.Amount > 0)
                .Select(effect => EconomyEffect.Credit(effect.Amount, effect.Reason))
                .ToArray();
            if (walletEffects.Length == 0) continue;

            var result = await context.ApplyWalletAsync(
                group.Key.UserId,
                group.Key.ChatId,
                walletEffects,
                $"{context.OperationId ?? $"pick:{Guid.NewGuid():N}"}:wallet-credit:{group.Key.UserId}:{group.Key.ChatId}",
                ct).ConfigureAwait(false);
            if (!result)
                throw new InvalidOperationException($"Wallet {group.Key.UserId}:{group.Key.ChatId} rejected a Pick credit.");
        }
    }
}
