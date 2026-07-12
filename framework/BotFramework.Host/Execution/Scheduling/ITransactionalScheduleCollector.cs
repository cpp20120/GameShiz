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
}
