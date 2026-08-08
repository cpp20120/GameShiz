using BotFramework.Sdk.Execution;

namespace BotFramework.Host.Execution;

internal interface IAtomicEconomics
{
    async Task<WalletSnapshot> EnsureAndLoadAsync(
        WalletIdentity wallet,
        string displayName,
        IGameExecutionSession session,
        CancellationToken ct)
    {
        await EnsureAsync(wallet, displayName, session, ct);
        return await LoadAsync(wallet, session, ct);
    }

    Task EnsureAsync(
        WalletIdentity wallet,
        string displayName,
        IGameExecutionSession session,
        CancellationToken ct);

    Task<WalletSnapshot> LoadAsync(
        WalletIdentity wallet,
        IGameExecutionSession session,
        CancellationToken ct);

    Task<WalletMutationResult> ApplyAsync(
        WalletIdentity wallet,
        IReadOnlyList<EconomyEffect> effects,
        IGameExecutionSession session,
        string operationId,
        CancellationToken ct);
}
