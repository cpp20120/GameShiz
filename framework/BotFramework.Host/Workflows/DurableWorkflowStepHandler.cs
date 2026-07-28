namespace BotFramework.Host.Workflows;

public sealed class DurableWorkflowStepHandler(IDurableWorkflowStepStore steps)
{
    public Task Handle(DurableWorkflowStep step, CancellationToken ct) => steps.UpsertAsync(step, ct);
}
