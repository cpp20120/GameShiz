namespace BotFramework.Host.Execution;

internal interface ITransactionalEventCollector
{
    Task AppendAsync(
        string commandId,
        IReadOnlyList<IDomainEvent> events,
        IGameExecutionSession session,
        CancellationToken ct);
}
