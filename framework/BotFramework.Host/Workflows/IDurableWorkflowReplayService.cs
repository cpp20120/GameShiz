namespace BotFramework.Host.Workflows;

public interface IDurableWorkflowReplayService
{
    Task<DurableWorkflowReplayResult> ReplayAsync(long stepId, CancellationToken ct);
}
