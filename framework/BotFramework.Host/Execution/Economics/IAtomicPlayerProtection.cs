using BotFramework.Sdk.Execution;

namespace BotFramework.Host.Execution;

internal interface IAtomicPlayerProtection
{
    Task EnforceAsync(
        long userId,
        IReadOnlyList<EconomyEffect> effects,
        IGameExecutionSession session,
        CancellationToken ct);
}
