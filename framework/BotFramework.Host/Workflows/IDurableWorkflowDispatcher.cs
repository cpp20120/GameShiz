namespace BotFramework.Host.Workflows;

public interface IDurableWorkflowDispatcher
{
    Task<TResult> DispatchAsync<TResult>(
        object command,
        DurableWorkflowDispatchOptions options,
        Func<TResult> pendingResult,
        CancellationToken ct);
}
