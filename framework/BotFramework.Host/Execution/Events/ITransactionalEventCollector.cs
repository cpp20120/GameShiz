using BotFramework.Contracts.Tenancy;

namespace BotFramework.Host.Execution;

internal interface ITransactionalEventCollector
{
    Task AppendAsync(
        string commandId,
        IReadOnlyList<IDomainEvent> events,
        IGameExecutionSession session,
        CancellationToken ct);

    Task AppendAsync(
        string commandId,
        IReadOnlyList<IDomainEvent> events,
        IGameExecutionSession session,
        TenantContext? tenantContext,
        CancellationToken ct) =>
        AppendAsync(commandId, events, session, ct);
}
