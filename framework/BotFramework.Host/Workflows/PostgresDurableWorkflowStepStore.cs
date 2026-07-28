using Dapper;

namespace BotFramework.Host.Workflows;

public sealed class PostgresDurableWorkflowStepStore(INpgsqlConnectionFactory connections) : IDurableWorkflowStepStore
{
    public async Task UpsertAsync(DurableWorkflowStep workflowStep, CancellationToken ct)
    {
        await using var connection = await connections.OpenAsync(ct);
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
            workflowStep,
            cancellationToken: ct));
    }

    public async Task<DurableWorkflowStep?> GetByCommandIdAsync(string commandId, CancellationToken ct)
    {
        await using var connection = await connections.OpenAsync(ct);
        return await connection.QuerySingleOrDefaultAsync<DurableWorkflowStep>(new CommandDefinition(
            SelectSql + " WHERE command_id = @commandId",
            new { commandId },
            cancellationToken: ct));
    }

    public async Task<DurableWorkflowStep?> GetByIdAsync(long id, CancellationToken ct)
    {
        await using var connection = await connections.OpenAsync(ct);
        return await connection.QuerySingleOrDefaultAsync<DurableWorkflowStep>(new CommandDefinition(
            SelectSql + " WHERE id = @id",
            new { id },
            cancellationToken: ct));
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
