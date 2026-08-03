using System.Data;
using System.Text.Json;
using BotFramework.Contracts.Messaging;
using BotFramework.Host.Persistence.Connections;
using Dapper;

namespace BotFramework.Host.Messaging;

/// <summary>
/// PostgreSQL-backed integration inbox. The inbox row is inserted, the
/// callback runs, and its result is stored in one local transaction. A
/// concurrent duplicate blocks on the unique key and then replays the stored
/// result without invoking the callback again.
/// </summary>
public sealed class PostgresIntegrationInbox(
    INpgsqlConnectionFactory connections,
    IntegrationInboxOptions options,
    IIntegrationInboxContextAccessor contextAccessor) : IIntegrationInbox
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IntegrationInboxResult<TResult>> ExecuteOnceAsync<TResult>(
        IntegrationInboxMessage message,
        Func<IntegrationInboxContext, CancellationToken, Task<TResult>> execute,
        CancellationToken ct)
    {
        Validate(message, execute);

        await using var connection = await connections.OpenAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);

        var inserted = await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO integration_inbox_messages (
                consumer_name, tenant_id, scope_id, message_id, message_type,
                contract_type, schema_version, payload, occurred_at,
                correlation_id, causation_id, player_id, channel, status)
            VALUES (
                @ConsumerName, @TenantId, @ScopeId, @MessageId, @MessageType,
                @ContractType, @SchemaVersion, CAST(@Payload AS jsonb), @OccurredAt,
                @CorrelationId, @CausationId, @PlayerId, @Channel, 'processing')
            ON CONFLICT (consumer_name, tenant_id, scope_id, message_id) DO NOTHING
            """,
            new
            {
                ConsumerName = options.ConsumerName,
                TenantId = message.TenantId ?? string.Empty,
                ScopeId = message.ScopeId ?? string.Empty,
                message.MessageId,
                message.MessageType,
                message.ContractType,
                message.SchemaVersion,
                message.Payload,
                message.OccurredAt,
                message.CorrelationId,
                message.CausationId,
                message.PlayerId,
                Channel = message.Channel.ToString(),
            },
            transaction,
            cancellationToken: ct));

        if (inserted == 0)
        {
            var existing = await connection.QuerySingleAsync<InboxRow>(new CommandDefinition(
                """
                SELECT status AS Status,
                       message_type AS MessageType,
                       contract_type AS ContractType,
                       result_type AS StoredResultType,
                       result_json::text AS ResultJson
                FROM integration_inbox_messages
                WHERE consumer_name = @ConsumerName
                  AND tenant_id = @TenantId
                  AND scope_id = @ScopeId
                  AND message_id = @MessageId
                FOR UPDATE
                """,
                new
                {
                    ConsumerName = options.ConsumerName,
                    TenantId = message.TenantId ?? string.Empty,
                    ScopeId = message.ScopeId ?? string.Empty,
                    message.MessageId,
                },
                transaction,
                cancellationToken: ct));

            ValidateExisting(existing, message);
            if (!string.Equals(existing.Status, "completed", StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"Integration message '{message.MessageId}' has an incomplete inbox entry.");

            var result = DeserializeResult<TResult>(existing, message.MessageId);
            await transaction.CommitAsync(ct);
            BotFramework.Contracts.Observability.BotFrameworkMetrics.IntegrationInboxDuplicates.Add(1);
            BotFramework.Contracts.Observability.BotFrameworkMetrics.IntegrationInboxResultReplays.Add(1);
            return new IntegrationInboxResult<TResult>(AlreadyProcessed: true, result);
        }

        var context = new IntegrationInboxContext(connection, transaction, message);
        using var contextScope = contextAccessor.Push(context);
        TResult resultValue;
        try
        {
            resultValue = await execute(context, ct);
        }
        catch
        {
            BotFramework.Contracts.Observability.BotFrameworkMetrics.IntegrationInboxHandlerFailures.Add(1);
            throw;
        }
        var resultJson = JsonSerializer.Serialize(resultValue, JsonOptions);

        var completed = await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE integration_inbox_messages
            SET status = 'completed',
                result_type = @ResultType,
                result_json = CAST(@ResultJson AS jsonb),
                completed_at = now()
            WHERE consumer_name = @ConsumerName
              AND tenant_id = @TenantId
              AND scope_id = @ScopeId
              AND message_id = @MessageId
              AND status = 'processing'
            """,
            new
            {
                ConsumerName = options.ConsumerName,
                TenantId = message.TenantId ?? string.Empty,
                ScopeId = message.ScopeId ?? string.Empty,
                message.MessageId,
                ResultType = ResultType<TResult>(),
                ResultJson = resultJson,
            },
            transaction,
            cancellationToken: ct));

        if (completed != 1)
            throw new InvalidOperationException(
                $"Integration message '{message.MessageId}' could not be completed in the inbox.");

        await transaction.CommitAsync(ct);
        return new IntegrationInboxResult<TResult>(AlreadyProcessed: false, resultValue);
    }

    public async Task<IntegrationInboxResult> ExecuteOnceAsync(
        IntegrationInboxMessage message,
        Func<IntegrationInboxContext, CancellationToken, Task> execute,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(execute);
        var result = await ExecuteOnceAsync<object?>(
            message,
            async (context, callbackCt) =>
            {
                await execute(context, callbackCt);
                return null;
            },
            ct);
        return new IntegrationInboxResult(result.AlreadyProcessed);
    }

    private static TResult? DeserializeResult<TResult>(InboxRow row, string messageId)
    {
        var expectedType = ResultType<TResult>();
        if (!string.Equals(row.StoredResultType, expectedType, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"Integration message '{messageId}' contains result type '{row.StoredResultType}', expected '{expectedType}'.");
        if (row.ResultJson is null || string.Equals(row.ResultJson, "null", StringComparison.OrdinalIgnoreCase))
            return default;

        return JsonSerializer.Deserialize<TResult>(row.ResultJson, JsonOptions)
            ?? throw new InvalidOperationException($"Integration message '{messageId}' has an invalid result.");
    }

    private static string ResultType<TResult>() => typeof(TResult).FullName ?? typeof(TResult).Name;

    private void Validate(IntegrationInboxMessage message, Delegate execute)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(execute);
        if (string.IsNullOrWhiteSpace(message.MessageId))
            throw new ArgumentException("Message id is required.", nameof(message));
        if (string.IsNullOrWhiteSpace(message.MessageType))
            throw new ArgumentException("Message type is required.", nameof(message));
        if (string.IsNullOrWhiteSpace(message.ContractType))
            throw new ArgumentException("Contract type is required.", nameof(message));
        if (string.IsNullOrWhiteSpace(options.ConsumerName))
            throw new InvalidOperationException("Integration inbox consumer name is required.");
    }

    private static void ValidateExisting(InboxRow existing, IntegrationInboxMessage message)
    {
        if (!string.Equals(existing.MessageType, message.MessageType, StringComparison.Ordinal)
            || !string.Equals(existing.ContractType, message.ContractType, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"Integration message '{message.MessageId}' was reused for another contract.");
    }

    private sealed record InboxRow(
        string Status,
        string MessageType,
        string ContractType,
        string? StoredResultType,
        string? ResultJson);
}
