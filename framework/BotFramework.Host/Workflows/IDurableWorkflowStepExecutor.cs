namespace BotFramework.Host.Workflows;

public interface IDurableWorkflowStepExecutor
{
    Task<TResult> ExecuteAsync<TResult>(
        object command,
        DurableWorkflowExecutionOptions options,
        Func<Task<TResult>> execute,
        Func<TResult, bool> succeeded,
        Func<TResult, bool> terminal,
        Func<TResult, string?> aggregateId,
        Func<TResult, object> payload,
        CancellationToken ct);
}
