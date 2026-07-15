using BotFramework.Host.Contracts.ResponsibleGaming;
using BotFramework.Sdk.Execution;

namespace BotFramework.Host.Execution;

internal sealed class RemoteAtomicPlayerProtection : IAtomicPlayerProtection
{
    public Task EnforceAsync(
        long userId,
        IReadOnlyList<EconomyEffect> effects,
        IGameExecutionSession session,
        CancellationToken ct)
    {
        // Enforcement is performed inside WalletAtomicExecutionService in the
        // same Wallet transaction as the balance mutation. There is no safe
        // Backend-side read that could replace that check.
        _ = userId;
        _ = effects;
        _ = session;
        _ = ct;
        return Task.CompletedTask;
    }
}
