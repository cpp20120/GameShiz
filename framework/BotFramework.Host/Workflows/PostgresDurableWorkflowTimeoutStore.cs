using System.Data;
using System.Text.Json;
using BotFramework.Contracts.Messaging;
using BotFramework.Host.Persistence.Connections;
using Dapper;

namespace BotFramework.Host.Workflows;

public sealed class PostgresDurableWorkflowTimeoutStore(INpgsqlConnectionFactory connections)
    : IDurableWorkflowTimeoutStore
{
    public async Task ScheduleAsync(
        DurableWorkflowTimeoutRequest request,
        IntegrationTransactionContext? transaction,
        CancellationToken ct)
    {
        request.Validate();
        var commandJson = JsonSerializer.Serialize(request.Command, DurableWorkflowJson.Options);
        var commandType = DurableWorkflowCommandTypes.Stable(request.Command.GetType());
        var parameters = new
        {
            request.TimeoutId,
            request.WorkflowId,
            request.CommandId,
            CommandType = commandType,
            request.Operation,
            request.AggregateId,
            request.CausationId,
            request.GroupId,
            CommandJson = commandJson,
            request.DueAt,
            request.MaxAttempts,
        };

        if (transaction is not null)
        {
            await InsertAsync(transaction.Connection, transaction.Transaction, parameters, ct);
            BotFramework.Contracts.Observability.BotFrameworkMetrics.WorkflowTimeoutsScheduled.Add(1);
            return;
        }

        await using var connection = await connections.OpenAsync(ct);
        await using var localTransaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        await InsertAsync(connection, localTransaction, parameters, ct);
        await localTransaction.CommitAsync(ct);
        BotFramework.Contracts.Observability.BotFrameworkMetrics.WorkflowTimeoutsScheduled.Add(1);
    }

    public async Task<IReadOnlyList<DurableWorkflowTimeout>> ClaimDueAsync(
        int limit,
        TimeSpan lease,
        string leaseOwner,
        CancellationToken ct)
    {
        if (limit <= 0)
            throw new ArgumentOutOfRangeException(nameof(limit));
        if (lease <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(lease));
        ArgumentException.ThrowIfNullOrWhiteSpace(leaseOwner);

        await using var connection = await connections.OpenAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        var rows = (await connection.QueryAsync<TimeoutRow>(new CommandDefinition(
            """
            WITH due AS (
                SELECT timeout_id
                FROM durable_workflow_timeouts
                WHERE due_at <= now()
                  AND (
                      status = 'pending'
                      OR (status = 'sending' AND locked_until < now())
                  )
                  AND attempts < max_attempts
                ORDER BY due_at, timeout_id
                FOR UPDATE SKIP LOCKED
                LIMIT @Limit
            )
            UPDATE durable_workflow_timeouts AS timeout
            SET status = 'sending',
                attempts = timeout.attempts + 1,
                locked_until = now() + @Lease,
                locked_by = @LeaseOwner
            FROM due
            WHERE timeout.timeout_id = due.timeout_id
            RETURNING timeout.timeout_id AS TimeoutId,
                      timeout.workflow_id AS WorkflowId,
                      timeout.command_id AS CommandId,
                      timeout.command_type AS CommandType,
                      timeout.operation AS Operation,
                      timeout.aggregate_id AS AggregateId,
                      timeout.causation_id AS CausationId,
                      timeout.group_id AS GroupId,
                      timeout.command_json::text AS CommandJson,
                      timeout.due_at AS DueAt,
                      timeout.status AS Status,
                      timeout.attempts AS Attempts,
                      timeout.max_attempts AS MaxAttempts,
                      timeout.created_at AS CreatedAt,
                      timeout.dispatched_at AS DispatchedAt,
                      timeout.last_error AS LastError
            """,
            new { Limit = limit, Lease = lease, LeaseOwner = leaseOwner },
            transaction,
            cancellationToken: ct)))
            .Select(Map)
            .ToArray();
        await transaction.CommitAsync(ct);
        return rows;
    }

    public async Task MarkDispatchedAsync(string timeoutId, string leaseOwner, CancellationToken ct)
    {
        await using var connection = await connections.OpenAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE durable_workflow_timeouts
            SET status = 'dispatched', dispatched_at = now(), locked_until = NULL, locked_by = NULL
            WHERE timeout_id = @TimeoutId AND status = 'sending' AND locked_by = @LeaseOwner
            """,
            new { TimeoutId = timeoutId, LeaseOwner = leaseOwner },
            cancellationToken: ct));
    }

    public async Task MarkFailedAsync(string timeoutId, string leaseOwner, string error, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        await using var connection = await connections.OpenAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE durable_workflow_timeouts
            SET status = CASE WHEN attempts >= max_attempts THEN 'failed' ELSE 'pending' END,
                due_at = CASE WHEN attempts >= max_attempts
                              THEN due_at
                              ELSE now() + (LEAST(300, GREATEST(5, attempts * attempts * 5)) * interval '1 second')
                         END,
                locked_until = NULL,
                locked_by = NULL,
                last_error = @Error
            WHERE timeout_id = @TimeoutId AND status = 'sending' AND locked_by = @LeaseOwner
            """,
            new { TimeoutId = timeoutId, LeaseOwner = leaseOwner, Error = error },
            cancellationToken: ct));
    }

    public async Task<bool> CancelAsync(string timeoutId, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(timeoutId);
        await using var connection = await connections.OpenAsync(ct);
        var affected = await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE durable_workflow_timeouts
            SET status = 'cancelled', locked_until = NULL, locked_by = NULL
            WHERE timeout_id = @TimeoutId AND status = 'pending'
            """,
            new { TimeoutId = timeoutId },
            cancellationToken: ct));
        return affected != 0;
    }

    public async Task<IReadOnlyList<DurableWorkflowTimeout>> GetByWorkflowAsync(
        string workflowId,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowId);
        await using var connection = await connections.OpenAsync(ct);
        var rows = await connection.QueryAsync<TimeoutRow>(new CommandDefinition(
            SelectSql + " WHERE workflow_id = @WorkflowId ORDER BY created_at, timeout_id",
            new { WorkflowId = workflowId },
            cancellationToken: ct));
        return rows.Select(Map).ToArray();
    }

    private static async Task InsertAsync(
        System.Data.Common.DbConnection connection,
        System.Data.Common.DbTransaction transaction,
        object parameters,
        CancellationToken ct)
    {
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO durable_workflow_timeouts
                (timeout_id, workflow_id, command_id, command_type, operation, aggregate_id,
                 causation_id, group_id, command_json, due_at, max_attempts)
            VALUES
                (@TimeoutId, @WorkflowId, @CommandId, @CommandType, @Operation, @AggregateId,
                 @CausationId, @GroupId, CAST(@CommandJson AS jsonb), @DueAt, @MaxAttempts)
            ON CONFLICT (timeout_id) DO NOTHING
            """,
            parameters,
            transaction,
            cancellationToken: ct));
    }

    private const string SelectSql = """
        SELECT timeout_id AS TimeoutId,
               workflow_id AS WorkflowId,
               command_id AS CommandId,
               command_type AS CommandType,
               operation AS Operation,
               aggregate_id AS AggregateId,
               causation_id AS CausationId,
               group_id AS GroupId,
               command_json::text AS CommandJson,
               due_at AS DueAt,
               status AS Status,
               attempts AS Attempts,
               max_attempts AS MaxAttempts,
               created_at AS CreatedAt,
               dispatched_at AS DispatchedAt,
               last_error AS LastError
        FROM durable_workflow_timeouts
        """;

    private static DurableWorkflowTimeout Map(TimeoutRow row) => new(
        row.TimeoutId,
        row.WorkflowId,
        row.CommandId,
        row.CommandType,
        row.Operation,
        row.AggregateId,
        row.CausationId,
        row.GroupId,
        row.CommandJson,
        new DateTimeOffset(DateTime.SpecifyKind(row.DueAt, DateTimeKind.Utc)),
        row.Status,
        row.Attempts,
        row.MaxAttempts,
        new DateTimeOffset(DateTime.SpecifyKind(row.CreatedAt, DateTimeKind.Utc)),
        row.DispatchedAt is { } dispatched
            ? new DateTimeOffset(DateTime.SpecifyKind(dispatched, DateTimeKind.Utc))
            : null,
        row.LastError);

    private sealed class TimeoutRow
    {
        public string TimeoutId { get; init; } = "";
        public string WorkflowId { get; init; } = "";
        public string CommandId { get; init; } = "";
        public string CommandType { get; init; } = "";
        public string Operation { get; init; } = "";
        public string? AggregateId { get; init; }
        public string? CausationId { get; init; }
        public string? GroupId { get; init; }
        public string CommandJson { get; init; } = "";
        public DateTime DueAt { get; init; }
        public string Status { get; init; } = "";
        public int Attempts { get; init; }
        public int MaxAttempts { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime? DispatchedAt { get; init; }
        public string? LastError { get; init; }
    }
}
