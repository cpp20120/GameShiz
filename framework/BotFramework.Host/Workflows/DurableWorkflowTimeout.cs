namespace BotFramework.Host.Workflows;

public sealed record DurableWorkflowTimeout(
    string TimeoutId,
    string WorkflowId,
    string CommandId,
    string CommandType,
    string Operation,
    string? AggregateId,
    string? CausationId,
    string? GroupId,
    string CommandJson,
    DateTimeOffset DueAt,
    string Status,
    int Attempts,
    int MaxAttempts,
    DateTimeOffset CreatedAt,
    DateTimeOffset? DispatchedAt = null,
    string? LastError = null);
