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

        var command = DeserializeCommand(step.CommandType, step.CommandJson);
        var replayId = $"admin:replay:{step.Id}:{Guid.NewGuid():N}";
        await bus.SendAsync(command, new DeliveryOptions
        {
            CorrelationId = step.WorkflowId,
            CausationId = replayId,
            GroupId = step.WorkflowId,
        });

        return new(true, true, $"Command {step.CommandId} was queued for replay.");
    }

    private static object DeserializeCommand(string commandType, string json)
    {
        var separator = commandType.IndexOf(':');
        if (separator <= 0 || separator == commandType.Length - 1)
            throw new InvalidOperationException($"Workflow command type '{commandType}' has an invalid stable name.");
        var assemblyName = commandType[..separator];
        var typeName = commandType[(separator + 1)..];
        var assembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(candidate => string.Equals(candidate.GetName().Name, assemblyName, StringComparison.Ordinal));
        var type = assembly?.GetType(typeName, throwOnError: false, ignoreCase: false)
            ?? throw new InvalidOperationException($"Workflow command type '{commandType}' is not available.");
        if (!typeof(IDurableWorkflowCommand).IsAssignableFrom(type))
            throw new InvalidOperationException($"Workflow command type '{commandType}' is not replayable.");
        return JsonSerializer.Deserialize(json, type, DurableWorkflowJson.Options)
            ?? throw new InvalidOperationException($"Workflow command '{commandType}' could not be deserialized.");
    }
}
