using System.Text.Json;
using Wolverine;

namespace BotFramework.Host.Workflows;

public sealed class DurableWorkflowDispatcher(
    IMessageBus bus,
    IDurableWorkflowStepStore steps) : IDurableWorkflowDispatcher
{
    public async Task<TResult> DispatchAsync<TResult>(
        object command,
        DurableWorkflowDispatchOptions options,
        Func<TResult> pendingResult,
        CancellationToken ct)
    {
        if (command is not IDurableWorkflowCommand)
            throw new ArgumentException($"Command {command.GetType().FullName} must implement {nameof(IDurableWorkflowCommand)}.", nameof(command));
        if (string.IsNullOrWhiteSpace(options.WorkflowId)
            || string.IsNullOrWhiteSpace(options.CommandId)
            || string.IsNullOrWhiteSpace(options.Operation))
            throw new ArgumentException("Workflow id, command id and operation are required.", nameof(options));

        await bus.SendAsync(command, new DeliveryOptions
        {
            CorrelationId = options.WorkflowId,
            CausationId = options.CausationId ?? options.CommandId,
            GroupId = options.GroupId ?? options.WorkflowId,
        });

        var timeout = options.WaitTimeout is { } configured && configured > TimeSpan.Zero
            ? configured
            : TimeSpan.FromSeconds(15);
        var deadline = DateTimeOffset.UtcNow.Add(timeout);
        while (DateTimeOffset.UtcNow < deadline)
        {
            var row = await steps.GetByCommandIdAsync(options.CommandId, ct);
            if (row is not null && row.Status is "completed" or "rejected")
            {
                if (string.IsNullOrWhiteSpace(row.ResultJson)
                    || string.Equals(row.ResultJson, "null", StringComparison.OrdinalIgnoreCase))
                    return default!;
                return JsonSerializer.Deserialize<TResult>(row.ResultJson, DurableWorkflowJson.Options)!;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), ct);
        }

        return pendingResult();
    }
}
