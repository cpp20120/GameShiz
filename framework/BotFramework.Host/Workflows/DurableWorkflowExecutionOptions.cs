namespace BotFramework.Host.Workflows;

public sealed record DurableWorkflowExecutionOptions(
    string WorkflowId,
    string CommandId,
    string Operation,
    string? AggregateId = null,
    string? CausationId = null);
