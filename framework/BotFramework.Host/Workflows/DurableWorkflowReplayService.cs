using System.Text.Json;
using Wolverine;

namespace BotFramework.Host.Workflows;

public sealed class DurableWorkflowReplayService(
    IDurableWorkflowStepStore steps,
    IMessageBus bus) : IDurableWorkflowReplayService
{
    public async Task<DurableWorkflowReplayResult> ReplayAsync(long stepId, CancellationToken ct)
    {
        var step = await steps.GetByIdAsync(stepId, ct);
        if (step is null)
            return new(false, false, "Workflow step not found.");
        if (step.Terminal)
            return new(true, false, "Terminal workflow step is already complete.");
        if (string.IsNullOrWhiteSpace(step.CommandJson) || string.IsNullOrWhiteSpace(step.CommandType))
            return new(true, false, "Workflow step has no replayable command.");

        var command = DurableWorkflowCommandDeserializer.Deserialize(step.CommandType, step.CommandJson);
        var replayId = $"admin:replay:{step.Id}:{Guid.NewGuid():N}";
        await bus.SendAsync(command, new DeliveryOptions
        {
            CorrelationId = step.WorkflowId,
            CausationId = replayId,
            GroupId = step.WorkflowId,
        });

        return new(true, true, $"Command {step.CommandId} was queued for replay.");
    }
}
