using BotFramework.Contracts.Tenancy;
using BotFramework.Sdk.Execution;

namespace BotFramework.Host.Execution;

internal interface ITransactionalScheduleCollector
{
    Task AppendAsync(
        string commandId,
        string gameId,
        string aggregateId,
        IReadOnlyList<ScheduleEffect> effects,
        IGameExecutionSession session,
        CancellationToken ct);

    Task AppendAsync(
        string commandId,
        string gameId,
        string aggregateId,
        IReadOnlyList<ScheduleEffect> effects,
        IGameExecutionSession session,
        TenantContext? tenantContext,
        CancellationToken ct) =>
        AppendAsync(commandId, gameId, aggregateId, effects, session, ct);
}
