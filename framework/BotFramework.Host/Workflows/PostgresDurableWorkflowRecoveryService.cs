using BotFramework.Contracts.Messaging;

namespace BotFramework.Host.Workflows;

public sealed class PostgresDurableWorkflowRecoveryService(
    IDurableWorkflowTimeoutStore timeouts) : IDurableWorkflowRecoveryService
{
    public Task ScheduleTimeoutAsync(
        DurableWorkflowTimeoutRequest request,
        IntegrationTransactionContext? transaction = null,
        CancellationToken ct = default) =>
        timeouts.ScheduleAsync(request, transaction, ct);

    public Task<bool> CancelTimeoutAsync(string timeoutId, CancellationToken ct = default) =>
        timeouts.CancelAsync(timeoutId, ct);

    public Task<IReadOnlyList<DurableWorkflowTimeout>> GetWorkflowTimeoutsAsync(
        string workflowId,
        CancellationToken ct = default) =>
        timeouts.GetByWorkflowAsync(workflowId, ct);
}
