using System.Data;
using BotFramework.Contracts.Messaging;
using BotFramework.Host.Persistence.Connections;
using Dapper;

namespace BotFramework.Host.Messaging;

public sealed class PostgresIntegrationOutbox(INpgsqlConnectionFactory connections)
    : IIntegrationOutboxStore
{
    public async Task EnqueueAsync(
        IntegrationOutboxMessage message,
        IntegrationTransactionContext? transaction,
        CancellationToken ct)
    {
        Validate(message);
        if (transaction is not null)
        {
            await InsertAsync(transaction.Connection, transaction.Transaction, message, ct);
            BotFramework.Contracts.Observability.BotFrameworkMetrics.IntegrationOutboxEnqueued.Add(1);
            return;
        }

        await using var connection = await connections.OpenAsync(ct);
        await using var localTransaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        await InsertAsync(connection, localTransaction, message, ct);
        await localTransaction.CommitAsync(ct);
        BotFramework.Contracts.Observability.BotFrameworkMetrics.IntegrationOutboxEnqueued.Add(1);
    }

    public async Task<IReadOnlyList<IntegrationOutboxDelivery>> ClaimAsync(
        string producerName,
        int limit,
        TimeSpan lease,
        string leaseOwner,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(producerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(leaseOwner);
        if (limit <= 0)
            throw new ArgumentOutOfRangeException(nameof(limit));
        if (lease <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(lease));

        await using var connection = await connections.OpenAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        var rows = (await connection.QueryAsync<OutboxRow>(new CommandDefinition(
            """
            WITH due AS (
                SELECT outbox_id
                FROM integration_outbox_messages
                WHERE producer_name = @ProducerName
                  AND next_attempt_at <= now()
                  AND (
                      status = 'pending'
                      OR (status = 'sending' AND locked_until < now())
                  )
                ORDER BY outbox_id
                FOR UPDATE SKIP LOCKED
                LIMIT @Limit
            )
            UPDATE integration_outbox_messages AS outbox
            SET status = 'sending',
                attempts = outbox.attempts + 1,
                locked_until = now() + @Lease,
                locked_by = @LeaseOwner
            FROM due
            WHERE outbox.outbox_id = due.outbox_id
            RETURNING outbox.outbox_id AS OutboxId,
                      outbox.producer_name AS ProducerName,
                      outbox.message_id AS MessageId,
                      outbox.kind AS Kind,
                      outbox.topic AS Topic,
                      outbox.message_key AS MessageKey,
                      outbox.message_type AS MessageType,
                      outbox.contract_type AS ContractType,
                      outbox.schema_version AS SchemaVersion,
                      outbox.payload::text AS Payload,
                      outbox.envelope_json::text AS EnvelopeJson,
                      outbox.occurred_at AS OccurredAt,
                      outbox.correlation_id AS CorrelationId,
                      outbox.causation_id AS CausationId,
                      outbox.tenant_id AS TenantId,
                      outbox.scope_id AS ScopeId,
                      outbox.player_id AS PlayerId,
                      outbox.channel AS Channel,
                      outbox.locked_by AS LeaseOwner,
                      outbox.attempts AS Attempts,
                      outbox.created_at AS CreatedAt
            """,
            new
            {
                ProducerName = producerName,
                Limit = limit,
                Lease = lease,
                LeaseOwner = leaseOwner,
            },
            transaction,
            cancellationToken: ct)))
            .Select(static row => new IntegrationOutboxDelivery(
                row.OutboxId,
                row.ProducerName,
                row.MessageId,
                Enum.Parse<IntegrationMessageKind>(row.Kind, ignoreCase: true),
                row.Topic,
                row.MessageKey,
                row.MessageType,
                row.ContractType,
                row.SchemaVersion,
                row.Payload,
                row.EnvelopeJson,
                new DateTimeOffset(DateTime.SpecifyKind(row.OccurredAt, DateTimeKind.Utc)),
                row.CorrelationId,
                row.CausationId,
                row.TenantId,
                row.ScopeId,
                row.PlayerId,
                Enum.Parse<BotChannel>(row.Channel, ignoreCase: true),
                row.LeaseOwner,
                row.Attempts,
                new DateTimeOffset(DateTime.SpecifyKind(row.CreatedAt, DateTimeKind.Utc))))
            .ToArray();
        await transaction.CommitAsync(ct);
        return rows;
    }

    public async Task MarkPublishedAsync(
        string producerName,
        string messageId,
        string leaseOwner,
        CancellationToken ct)
    {
        await using var connection = await connections.OpenAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE integration_outbox_messages
            SET status = 'sent', published_at = now(), locked_until = NULL, locked_by = NULL
            WHERE producer_name = @ProducerName
              AND message_id = @MessageId
              AND status = 'sending'
              AND locked_by = @LeaseOwner
            """,
            new { ProducerName = producerName, MessageId = messageId, LeaseOwner = leaseOwner },
            cancellationToken: ct));
    }

    public async Task MarkFailedAsync(
        string producerName,
        string messageId,
        string leaseOwner,
        string error,
        CancellationToken ct)
    {
        await using var connection = await connections.OpenAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE integration_outbox_messages
            SET status = 'pending',
                next_attempt_at = now() + make_interval(secs => LEAST(300, GREATEST(5, attempts * attempts * 5))),
                locked_until = NULL,
                locked_by = NULL,
                last_error = @Error
            WHERE producer_name = @ProducerName
              AND message_id = @MessageId
              AND status = 'sending'
              AND locked_by = @LeaseOwner
            """,
            new
            {
                ProducerName = producerName,
                MessageId = messageId,
                LeaseOwner = leaseOwner,
                Error = error.Length > 4000 ? error[..4000] : error,
            },
            cancellationToken: ct));
    }

    public async Task<long> CountPendingAsync(string producerName, CancellationToken ct)
    {
        await using var connection = await connections.OpenAsync(ct);
        return await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            """
            SELECT count(*)
            FROM integration_outbox_messages
            WHERE producer_name = @ProducerName
              AND status IN ('pending', 'sending')
            """,
            new { ProducerName = producerName },
            cancellationToken: ct));
    }

    private static async Task InsertAsync(
        System.Data.Common.DbConnection connection,
        System.Data.Common.DbTransaction transaction,
        IntegrationOutboxMessage message,
        CancellationToken ct)
    {
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO integration_outbox_messages (
                producer_name, message_id, kind, topic, message_key,
                message_type, contract_type, schema_version, payload, envelope_json,
                occurred_at, correlation_id, causation_id, tenant_id, scope_id,
                player_id, channel)
            VALUES (
                @ProducerName, @MessageId, @Kind, @Topic, @MessageKey,
                @MessageType, @ContractType, @SchemaVersion,
                CAST(@Payload AS jsonb), CAST(@EnvelopeJson AS jsonb),
                @OccurredAt, @CorrelationId, @CausationId, @TenantId, @ScopeId,
                @PlayerId, @Channel)
            ON CONFLICT (producer_name, message_id) DO NOTHING
            """,
            new
            {
                message.ProducerName,
                message.MessageId,
                Kind = message.Kind.ToString().ToLowerInvariant(),
                message.Topic,
                message.MessageKey,
                message.MessageType,
                message.ContractType,
                message.SchemaVersion,
                message.Payload,
                message.EnvelopeJson,
                message.OccurredAt,
                message.CorrelationId,
                message.CausationId,
                message.TenantId,
                message.ScopeId,
                message.PlayerId,
                Channel = message.Channel.ToString(),
            },
            transaction,
            cancellationToken: ct));
    }

    private static void Validate(IntegrationOutboxMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (string.IsNullOrWhiteSpace(message.ProducerName)
            || string.IsNullOrWhiteSpace(message.MessageId)
            || string.IsNullOrWhiteSpace(message.Topic)
            || string.IsNullOrWhiteSpace(message.MessageKey)
            || string.IsNullOrWhiteSpace(message.MessageType)
            || string.IsNullOrWhiteSpace(message.ContractType)
            || string.IsNullOrWhiteSpace(message.Payload)
            || string.IsNullOrWhiteSpace(message.EnvelopeJson))
            throw new ArgumentException("Integration outbox message contains missing required fields.", nameof(message));
    }

    private sealed class OutboxRow
    {
        public long OutboxId { get; init; }
        public string Kind { get; init; } = "";
        public string ProducerName { get; init; } = "";
        public string MessageId { get; init; } = "";
        public string Topic { get; init; } = "";
        public string MessageKey { get; init; } = "";
        public string MessageType { get; init; } = "";
        public string ContractType { get; init; } = "";
        public int SchemaVersion { get; init; }
        public string Payload { get; init; } = "";
        public string EnvelopeJson { get; init; } = "";
        public DateTime OccurredAt { get; init; }
        public string CorrelationId { get; init; } = "";
        public string CausationId { get; init; } = "";
        public string? TenantId { get; init; }
        public string? ScopeId { get; init; }
        public string? PlayerId { get; init; }
        public string Channel { get; init; } = "";
        public string LeaseOwner { get; init; } = "";
        public int Attempts { get; init; }
        public DateTime CreatedAt { get; init; }
    }
}
