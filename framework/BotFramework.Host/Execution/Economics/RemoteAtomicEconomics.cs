using BotFramework.Host.Contracts.Economics;
using BotFramework.Sdk.Execution;

namespace BotFramework.Host.Execution;

/// <summary>
/// Backend-side adapter. All wallet mutation SQL lives behind the Wallet gRPC
/// atomic batch boundary when the microservices profile is active.
/// </summary>
internal sealed class RemoteAtomicEconomics(
    IEconomicsService economics,
    IWalletAtomicExecutionService walletService,
    IWalletSnapshotService? snapshots = null) : IAtomicEconomics
{
    public async Task EnsureAsync(
        WalletIdentity wallet,
        string displayName,
        IGameExecutionSession session,
        CancellationToken ct)
    {
        if (snapshots is not null)
        {
            _ = await snapshots.EnsureAndGetBalanceAsync(
                wallet.UserId, wallet.BalanceScopeId, displayName, ct);
            return;
        }

        await economics.EnsureUserAsync(wallet.UserId, wallet.BalanceScopeId, displayName, ct);
    }

    public async Task<WalletSnapshot> EnsureAndLoadAsync(
        WalletIdentity wallet,
        string displayName,
        IGameExecutionSession session,
        CancellationToken ct)
    {
        if (snapshots is not null)
        {
            var balance = await snapshots.EnsureAndGetBalanceAsync(
                wallet.UserId, wallet.BalanceScopeId, displayName, ct);
            return new WalletSnapshot(balance);
        }

        await economics.EnsureUserAsync(wallet.UserId, wallet.BalanceScopeId, displayName, ct);
        return new WalletSnapshot(
            (long)await economics.GetBalanceAsync(wallet.UserId, wallet.BalanceScopeId, ct));
    }

    public async Task<WalletSnapshot> LoadAsync(
        WalletIdentity wallet,
        IGameExecutionSession session,
        CancellationToken ct)
        => new((long)await economics.GetBalanceAsync(wallet.UserId, wallet.BalanceScopeId, ct));

    public async Task<WalletMutationResult> ApplyAsync(
        WalletIdentity wallet,
        IReadOnlyList<EconomyEffect> effects,
        IGameExecutionSession session,
        string operationId,
        CancellationToken ct)
    {
        var result = await walletService.ApplyBatchAsync(
            wallet.UserId,
            wallet.BalanceScopeId,
            effects.Select(effect => new WalletBatchEffect(
                effect.Kind switch
                {
                    EconomyEffectKind.Debit => WalletBatchEffectKind.Debit,
                    EconomyEffectKind.Credit => WalletBatchEffectKind.Credit,
                    _ => throw new ArgumentOutOfRangeException(nameof(effect), effect.Kind, "Unknown economy effect kind."),
                },
                checked((int)effect.Amount),
                effect.Reason)).ToArray(),
            operationId,
            ct);
        return new WalletMutationResult(result.Applied, result.Rejected, new WalletSnapshot(result.NewBalance));
    }
}
