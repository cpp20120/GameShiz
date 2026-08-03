using BotFramework.Contracts.Messaging;

namespace BotFramework.Host.Workflows;

/// <summary>
/// Generic recovery surface for saga implementations. Domain services keep
/// their own state and commands; the framework owns durable timeout delivery,
/// retry and cancellation.
/// </summary>
public interface IDurableWorkflowRecoveryService
{
    Task ScheduleTimeoutAsync(
        DurableWorkflowTimeoutRequest request,
        IntegrationTransactionContext? transaction = null,
        CancellationToken ct = default);

    Task<bool> CancelTimeoutAsync(string timeoutId, CancellationToken ct = default);

    Task<IReadOnlyList<DurableWorkflowTimeout>> GetWorkflowTimeoutsAsync(
        string workflowId,
        CancellationToken ct = default);
}
