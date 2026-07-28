namespace BotFramework.Host.Workflows;

public interface IDurableWorkflowStepStore
{
    Task UpsertAsync(DurableWorkflowStep workflowStep, CancellationToken ct);
    Task<DurableWorkflowStep?> GetByCommandIdAsync(string commandId, CancellationToken ct);
    Task<DurableWorkflowStep?> GetByIdAsync(long id, CancellationToken ct);
}
