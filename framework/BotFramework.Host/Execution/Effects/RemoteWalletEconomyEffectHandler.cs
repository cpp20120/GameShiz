using BotFramework.Host.Contracts.Economics;
using BotFramework.Sdk.Execution;

namespace BotFramework.Host.Execution;

internal sealed class RemoteWalletEconomyEffectHandler(
    IWalletAtomicExecutionService wallet) : GameEffectHandler<WalletEconomyEffect>
{
    protected override async Task ApplyBatchAsync(
        IReadOnlyList<WalletEconomyEffect> effects,
        IGameExecutionContext context,
        CancellationToken ct)
    {
        var operationPrefix = context.OperationId ?? $"remote-effect:{Guid.NewGuid():N}";
        var groupIndex = 0;
        foreach (var group in effects.GroupBy(effect => (effect.UserId, effect.BalanceScopeId)))
        {
            var result = await wallet.ApplyBatchAsync(
                group.Key.UserId,
                group.Key.BalanceScopeId,
                group.Select(effect => new WalletBatchEffect(
                    effect.Kind switch
                    {
                        EconomyEffectKind.Debit => WalletBatchEffectKind.Debit,
                        EconomyEffectKind.Credit => WalletBatchEffectKind.Credit,
                        _ => throw new ArgumentOutOfRangeException(nameof(effects), effect.Kind, "Unknown wallet effect kind."),
                    },
                    checked((int)effect.Amount),
                    effect.Reason)).ToArray(),
                $"{operationPrefix}:wallet-effect:{groupIndex++}",
                ct).ConfigureAwait(false);
            if (result.Rejected)
                throw new InvalidOperationException(
                    $"Wallet {group.Key.UserId}:{group.Key.BalanceScopeId} rejected an economy effect.");
        }
    }
}
