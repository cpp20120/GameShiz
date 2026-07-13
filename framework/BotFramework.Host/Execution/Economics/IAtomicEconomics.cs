using BotFramework.Sdk.Execution;

namespace BotFramework.Host.Execution;

internal interface IAtomicEconomics
{
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
        CancellationToken ct);
}
