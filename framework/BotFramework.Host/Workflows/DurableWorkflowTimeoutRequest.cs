namespace BotFramework.Host.Workflows;

/// <summary>
/// A durable, idempotently scheduled command for a workflow saga. The
/// command is persisted before the caller's transaction commits and is sent
/// by the framework timeout worker when it becomes due.
/// </summary>
public sealed record DurableWorkflowTimeoutRequest(
    string TimeoutId,
    string WorkflowId,
    string CommandId,
    string Operation,
    DateTimeOffset DueAt,
    IDurableWorkflowCommand Command,
    string? AggregateId = null,
    string? CausationId = null,
    string? GroupId = null,
    int MaxAttempts = 10)
{
    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(TimeoutId);
        ArgumentException.ThrowIfNullOrWhiteSpace(WorkflowId);
        ArgumentException.ThrowIfNullOrWhiteSpace(CommandId);
        ArgumentException.ThrowIfNullOrWhiteSpace(Operation);
        ArgumentNullException.ThrowIfNull(Command);
        if (MaxAttempts <= 0)
            throw new ArgumentOutOfRangeException(nameof(MaxAttempts));
    }
}
