namespace BotFramework.Host.Workflows;

public sealed record DurableWorkflowDispatchOptions(
    string WorkflowId,
    string CommandId,
    string Operation,
    string? AggregateId = null,
    string? CausationId = null,
    string? GroupId = null,
    TimeSpan? WaitTimeout = null);
