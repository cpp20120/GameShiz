using System.Text.Json;
using Dapper;
using Wolverine;
using Wolverine.Persistence.Sagas;

namespace BotFramework.Host.Workflows;

internal static class DurableWorkflowJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
    };
}

public sealed class PostgresDurableWorkflowStepStore(INpgsqlConnectionFactory connections) : IDurableWorkflowStepStore
{
    public async Task UpsertAsync(DurableWorkflowStep step, CancellationToken ct)
    {
        await using var connection = await connections.OpenAsync(ct).ConfigureAwait(false);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO durable_workflow_steps
                (workflow_id, command_id, command_type, aggregate_id, operation, status, terminal,
                 causation_id, command_json, payload, result, error, occurred_at)
            VALUES
                (@WorkflowId, @CommandId, @CommandType, @AggregateId, @Operation, @Status, @Terminal,
                 @CausationId, CAST(@CommandJson AS jsonb), CAST(@PayloadJson AS jsonb),
                 CAST(@ResultJson AS jsonb), @Error, @OccurredAt)
            ON CONFLICT (command_id) DO UPDATE SET
                workflow_id = EXCLUDED.workflow_id,
                command_type = EXCLUDED.command_type,
                aggregate_id = EXCLUDED.aggregate_id,
                operation = EXCLUDED.operation,
                status = EXCLUDED.status,
                terminal = EXCLUDED.terminal,
                causation_id = EXCLUDED.causation_id,
                command_json = EXCLUDED.command_json,
                payload = EXCLUDED.payload,
                result = EXCLUDED.result,
                error = EXCLUDED.error,
                occurred_at = EXCLUDED.occurred_at
            """,
            step,
            cancellationToken: ct)).ConfigureAwait(false);
    }

    public async Task<DurableWorkflowStep?> GetByCommandIdAsync(string commandId, CancellationToken ct)
    {
        await using var connection = await connections.OpenAsync(ct).ConfigureAwait(false);
        return await connection.QuerySingleOrDefaultAsync<DurableWorkflowStep>(new CommandDefinition(
            SelectSql + " WHERE command_id = @commandId",
            new { commandId },
            cancellationToken: ct)).ConfigureAwait(false);
    }

    public async Task<DurableWorkflowStep?> GetByIdAsync(long id, CancellationToken ct)
    {
        await using var connection = await connections.OpenAsync(ct).ConfigureAwait(false);
        return await connection.QuerySingleOrDefaultAsync<DurableWorkflowStep>(new CommandDefinition(
            SelectSql + " WHERE id = @id",
            new { id },
            cancellationToken: ct)).ConfigureAwait(false);
    }

    private const string SelectSql = """
        SELECT id AS Id,
               workflow_id AS WorkflowId,
               command_id AS CommandId,
               command_type AS CommandType,
               operation AS Operation,
               status AS Status,
               terminal AS Terminal,
               aggregate_id AS AggregateId,
               causation_id AS CausationId,
               command_json::text AS CommandJson,
               payload::text AS PayloadJson,
               result::text AS ResultJson,
               error AS Error,
               occurred_at AS OccurredAt
        FROM durable_workflow_steps
        """;
}

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
            var result = await execute().ConfigureAwait(false);
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
                ct).ConfigureAwait(false);
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
                CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }

    private async Task PublishStepAsync(DurableWorkflowStep step, CancellationToken ct)
    {
        await steps.UpsertAsync(step, ct).ConfigureAwait(false);
        await bus.PublishAsync(step, new DeliveryOptions
        {
            CorrelationId = step.WorkflowId,
            CausationId = step.CausationId ?? step.CommandId,
            GroupId = step.WorkflowId,
            SagaId = step.WorkflowId,
        }).ConfigureAwait(false);
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
        }).ConfigureAwait(false);

        var timeout = options.WaitTimeout is { } configured && configured > TimeSpan.Zero
            ? configured
            : TimeSpan.FromSeconds(15);
        var deadline = DateTimeOffset.UtcNow.Add(timeout);
        while (DateTimeOffset.UtcNow < deadline)
        {
            var row = await steps.GetByCommandIdAsync(options.CommandId, ct).ConfigureAwait(false);
            if (row is not null && row.Status is "completed" or "rejected")
            {
                if (string.IsNullOrWhiteSpace(row.ResultJson)
                    || string.Equals(row.ResultJson, "null", StringComparison.OrdinalIgnoreCase))
                    return default!;
                return JsonSerializer.Deserialize<TResult>(row.ResultJson, DurableWorkflowJson.Options)!;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), ct).ConfigureAwait(false);
        }

        return pendingResult();
    }
}

public sealed class DurableWorkflowReplayService(
    IDurableWorkflowStepStore steps,
    IMessageBus bus) : IDurableWorkflowReplayService
{
    public async Task<DurableWorkflowReplayResult> ReplayAsync(long stepId, CancellationToken ct)
    {
        var step = await steps.GetByIdAsync(stepId, ct).ConfigureAwait(false);
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
        }).ConfigureAwait(false);

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

internal static class DurableWorkflowCommandTypes
{
    public static string Stable(Type type) =>
        $"{type.Assembly.GetName().Name}:{type.FullName}";
}

public sealed class DurableWorkflowStepHandler(IDurableWorkflowStepStore steps)
{
    public Task Handle(DurableWorkflowStep step, CancellationToken ct) => steps.UpsertAsync(step, ct);
}

/// <summary>
/// Generic durable saga state for all workflow types. Domain-specific state
/// remains in the module; this state only tracks the operator-visible flow.
/// </summary>
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
