namespace CasinoShiz.Host.Pages.Admin;

public sealed record MetaTournamentWorkflowStepRow(
    long Id,
    string WorkflowId,
    string CommandId,
    string CommandType,
    string? AggregateId,
    string Operation,
    string Status,
    bool Terminal,
    string? CausationId,
    string CommandJson,
    string PayloadJson,
    string? Error,
    DateTimeOffset OccurredAt);
