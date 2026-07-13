using BotFramework.Sdk.Execution;

namespace BotFramework.Host.Execution;

internal interface IAtomicQuotaStore
{
    Task<QuotaSnapshot> LoadAsync(
        QuotaIdentity quota,
        IGameExecutionSession session,
        CancellationToken ct);

    Task<QuotaSnapshot> ApplyAsync(
        QuotaIdentity quota,
        IReadOnlyList<QuotaEffect> effects,
        IGameExecutionSession session,
        CancellationToken ct);
}
