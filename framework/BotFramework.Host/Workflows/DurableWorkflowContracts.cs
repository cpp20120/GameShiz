namespace BotFramework.Host.Workflows;

/// <summary>
/// Marker for commands that may be persisted and replayed by the durable
/// workflow host. Commands should be immutable records with bounded payloads.
/// </summary>
public interface IDurableWorkflowCommand
{
}

public sealed record DurableWorkflowStep(
    string WorkflowId,
    string CommandId,
    string CommandType,
    string Operation,
    string Status,
    bool Terminal,
    string? AggregateId,
    string? CausationId,
    string CommandJson,
    string PayloadJson,
    string? ResultJson,
    string? Error,
    DateTimeOffset OccurredAt,
    long Id = 0);

public sealed record DurableWorkflowDispatchOptions(
    string WorkflowId,
    string CommandId,
    string Operation,
    string? AggregateId = null,
    string? CausationId = null,
    string? GroupId = null,
    TimeSpan? WaitTimeout = null);

public sealed record DurableWorkflowExecutionOptions(
    string WorkflowId,
    string CommandId,
    string Operation,
    string? AggregateId = null,
    string? CausationId = null);

public sealed record DurableWorkflowReplayResult(bool Found, bool Enqueued, string Message);

public interface IDurableWorkflowDispatcher
{
    Task<TResult> DispatchAsync<TResult>(
        object command,
        DurableWorkflowDispatchOptions options,
        Func<TResult> pendingResult,
        CancellationToken ct);
}

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

public interface IDurableWorkflowStepStore
{
    Task UpsertAsync(DurableWorkflowStep step, CancellationToken ct);
    Task<DurableWorkflowStep?> GetByCommandIdAsync(string commandId, CancellationToken ct);
    Task<DurableWorkflowStep?> GetByIdAsync(long id, CancellationToken ct);
}

public interface IDurableWorkflowReplayService
{
    Task<DurableWorkflowReplayResult> ReplayAsync(long stepId, CancellationToken ct);
}
