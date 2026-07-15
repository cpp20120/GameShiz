using BotFramework.Host.Contracts.Economics;
using BotFramework.Sdk.Execution;

namespace BotFramework.Host.Execution;

/// <summary>
/// Backend-side adapter. All wallet mutation SQL lives behind the Wallet gRPC
/// atomic batch boundary when the microservices profile is active.
/// </summary>
internal sealed class RemoteAtomicEconomics(
    IEconomicsService economics,
    IWalletAtomicExecutionService wallet) : IAtomicEconomics
{
    public Task EnsureAsync(
        WalletIdentity walletIdentity,
        string displayName,
        IGameExecutionSession session,
        CancellationToken ct) =>
        economics.EnsureUserAsync(walletIdentity.UserId, walletIdentity.BalanceScopeId, displayName, ct);

    public async Task<WalletSnapshot> LoadAsync(
        WalletIdentity walletIdentity,
        IGameExecutionSession session,
        CancellationToken ct)
        => new((long)await economics.GetBalanceAsync(walletIdentity.UserId, walletIdentity.BalanceScopeId, ct));

    public async Task<WalletMutationResult> ApplyAsync(
        WalletIdentity walletIdentity,
        IReadOnlyList<EconomyEffect> effects,
        IGameExecutionSession session,
        string operationId,
        CancellationToken ct)
    {
        var result = await wallet.ApplyBatchAsync(
            walletIdentity.UserId,
            walletIdentity.BalanceScopeId,
            effects.Select(effect => new WalletBatchEffect(
                effect.Kind switch
                {
                    EconomyEffectKind.Debit => WalletBatchEffectKind.Debit,
                    EconomyEffectKind.Credit => WalletBatchEffectKind.Credit,
                    _ => throw new ArgumentOutOfRangeException(nameof(effects), effect.Kind, "Unknown economy effect kind."),
                },
                checked((int)effect.Amount),
                effect.Reason)).ToArray(),
            operationId,
            ct).ConfigureAwait(false);
        return new WalletMutationResult(result.Applied, result.Rejected, new WalletSnapshot(result.NewBalance));
    }
}
