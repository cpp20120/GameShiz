using Wolverine;
using Wolverine.Attributes;
using Wolverine.Persistence.Sagas;

namespace BotFramework.Host.Workflows;

/// <summary>
/// Generic durable saga state for all workflow types. Domain-specific state
/// remains in the module; this state only tracks the operator-visible flow.
/// </summary>
[WolverineIgnore]
public sealed class DurableWorkflowSaga : Saga
{
    [SagaIdentity]
    public string? Id { get; set; }
    public string? LastCommandId { get; set; }
    public string? LastOperation { get; set; }
    public string? LastStatus { get; set; }
    public int StepCount { get; set; }
    public DateTimeOffset LastOccurredAt { get; set; }

    public static DurableWorkflowSaga Start(DurableWorkflowStep step)
    {
        var saga = new DurableWorkflowSaga
        {
            Id = step.WorkflowId,
            LastCommandId = step.CommandId,
            LastOperation = step.Operation,
            LastStatus = step.Status,
            StepCount = 1,
            LastOccurredAt = step.OccurredAt,
        };
        if (step.Terminal)
            saga.MarkCompleted();
        return saga;
    }

    public void Handle(DurableWorkflowStep step)
    {
        LastCommandId = step.CommandId;
        LastOperation = step.Operation;
        LastStatus = step.Status;
        StepCount++;
        LastOccurredAt = step.OccurredAt;
        if (step.Terminal)
            MarkCompleted();
    }

    public static void NotFound(DurableWorkflowStep step)
    {
        // The audit row remains replayable even if Wolverine has removed a
        // completed saga instance.
    }
}
