namespace BotFramework.Host.Workflows;

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
