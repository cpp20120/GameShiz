using BotFramework.Contracts.Messaging;

namespace BotFramework.Host.Workflows;

public interface IDurableWorkflowTimeoutStore
{
    Task ScheduleAsync(
        DurableWorkflowTimeoutRequest request,
        IntegrationTransactionContext? transaction,
        CancellationToken ct);

    Task<IReadOnlyList<DurableWorkflowTimeout>> ClaimDueAsync(
        int limit,
        TimeSpan lease,
        string leaseOwner,
        CancellationToken ct);

    Task MarkDispatchedAsync(string timeoutId, string leaseOwner, CancellationToken ct);

    Task MarkFailedAsync(string timeoutId, string leaseOwner, string error, CancellationToken ct);

    Task<bool> CancelAsync(string timeoutId, CancellationToken ct);

    Task<IReadOnlyList<DurableWorkflowTimeout>> GetByWorkflowAsync(
        string workflowId,
        CancellationToken ct);
}
