using System.Text.Json;
using Wolverine;

namespace BotFramework.Host.Workflows;

public sealed class DurableWorkflowStepExecutor(
    IDurableWorkflowStepStore steps,
    IMessageBus bus) : IDurableWorkflowStepExecutor
{
    public async Task<TResult> ExecuteAsync<TResult>(
        object command,
        DurableWorkflowExecutionOptions options,
        Func<Task<TResult>> execute,
        Func<TResult, bool> succeeded,
        Func<TResult, bool> terminal,
        Func<TResult, string?> aggregateId,
        Func<TResult, object> payload,
        CancellationToken ct)
    {
        Validate(command, options);
        var commandJson = JsonSerializer.Serialize(command, DurableWorkflowJson.Options);
        var commandType = DurableWorkflowCommandTypes.Stable(command.GetType());
        var causationId = options.CausationId ?? options.CommandId;

        try
        {
            var result = await execute();
            await PublishStepAsync(new DurableWorkflowStep(
                options.WorkflowId,
                options.CommandId,
                commandType,
                options.Operation,
                succeeded(result) ? "completed" : "rejected",
                terminal(result),
                aggregateId(result),
                causationId,
                commandJson,
                JsonSerializer.Serialize(payload(result), DurableWorkflowJson.Options),
                JsonSerializer.Serialize(result, DurableWorkflowJson.Options),
                null,
                DateTimeOffset.UtcNow),
                ct);
            return result;
        }
        catch (Exception exception)
        {
            await PublishStepAsync(new DurableWorkflowStep(
                options.WorkflowId,
                options.CommandId,
                commandType,
                options.Operation,
                "failed",
                false,
                options.AggregateId,
                causationId,
                commandJson,
                JsonSerializer.Serialize(new { exception = exception.GetType().FullName }, DurableWorkflowJson.Options),
                null,
                exception.Message,
                DateTimeOffset.UtcNow),
                CancellationToken.None);
            throw;
        }
    }

    private async Task PublishStepAsync(DurableWorkflowStep step, CancellationToken ct)
    {
        await steps.UpsertAsync(step, ct);
        await bus.PublishAsync(step, new DeliveryOptions
        {
            CorrelationId = step.WorkflowId,
            CausationId = step.CausationId ?? step.CommandId,
            GroupId = step.WorkflowId,
            SagaId = step.WorkflowId,
        });
    }

    private static void Validate(object command, DurableWorkflowExecutionOptions options)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command is not IDurableWorkflowCommand)
            throw new ArgumentException($"Command {command.GetType().FullName} must implement {nameof(IDurableWorkflowCommand)}.", nameof(command));
        if (string.IsNullOrWhiteSpace(options.WorkflowId)
            || string.IsNullOrWhiteSpace(options.CommandId)
            || string.IsNullOrWhiteSpace(options.Operation))
            throw new ArgumentException("Workflow id, command id and operation are required.", nameof(options));
    }
}
