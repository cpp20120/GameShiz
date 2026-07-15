using Dapper;
using BotFramework.Contracts.Observability;

namespace BotFramework.Host.Execution;

internal sealed class PostgresGameEventOutbox(INpgsqlConnectionFactory connections)
{
    public async Task<IReadOnlyList<GameEventOutboxItem>> ClaimAsync(
        int limit,
        TimeSpan lease,
        CancellationToken ct)
    {
        await using var connection = await connections.OpenAsync(ct).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(ct).ConfigureAwait(false);
        var rows = await connection.QueryAsync<GameEventOutboxItem>(new CommandDefinition(
            """
            WITH due AS (
                SELECT id
                FROM game_event_outbox
                WHERE (status = 'pending' AND next_attempt_at <= now())
                   OR (status = 'sending' AND locked_until <= now())
                ORDER BY next_attempt_at, id
                LIMIT @limit
                FOR UPDATE SKIP LOCKED
            )
            UPDATE game_event_outbox AS outbox
            SET status = 'sending',
                attempts = outbox.attempts + 1,
                locked_until = now() + (@leaseMs * interval '1 millisecond')
            FROM due
            WHERE outbox.id = due.id
            RETURNING outbox.id AS Id,
                      outbox.type_name AS TypeName,
                      outbox.payload::text AS Payload,
                      outbox.attempts AS Attempts,
                      outbox.created_at AS CreatedAt
            """,
            new
            {
                limit = Math.Clamp(limit, 1, 100),
                leaseMs = Math.Max(1, lease.TotalMilliseconds),
            },
            transaction,
            cancellationToken: ct)).ConfigureAwait(false);
        await transaction.CommitAsync(ct).ConfigureAwait(false);
        await RecordDepthAsync(connection, ct).ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task<IReadOnlyList<TenantGameEventOutboxItem>> ClaimTenantAsync(
        int limit,
        TimeSpan lease,
        CancellationToken ct)
    {
        await using var connection = await connections.OpenAsync(ct).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(ct).ConfigureAwait(false);
        var rows = await connection.QueryAsync<TenantGameEventOutboxItem>(new CommandDefinition(
            """
            WITH due AS (
                SELECT id
                FROM tenant_event_outbox
                WHERE (status = 'pending' AND next_attempt_at <= now())
                   OR (status = 'sending' AND locked_until <= now())
                ORDER BY next_attempt_at, id
                LIMIT @limit
                FOR UPDATE SKIP LOCKED
            )
            UPDATE tenant_event_outbox AS outbox
            SET status = 'sending',
                attempts = outbox.attempts + 1,
                locked_until = now() + (@leaseMs * interval '1 millisecond')
            FROM due
            WHERE outbox.id = due.id
            RETURNING outbox.id AS Id,
                      (SELECT tenant_id FROM tenants WHERE tenant_key = outbox.tenant_key) AS TenantId,
                      (SELECT scope_id FROM tenant_scopes
                       WHERE tenant_key = outbox.tenant_key AND scope_key = outbox.scope_key) AS ScopeId,
                      outbox.player_id AS PlayerId,
                      outbox.request_id AS RequestId,
                      outbox.correlation_id AS CorrelationId,
                      outbox.channel AS Channel,
                      outbox.type_name AS TypeName,
                      outbox.payload::text AS Payload,
                      outbox.attempts AS Attempts,
                      outbox.created_at AS CreatedAt
            """,
            new
            {
                limit = Math.Clamp(limit, 1, 100),
                leaseMs = Math.Max(1, lease.TotalMilliseconds),
            },
            transaction,
            cancellationToken: ct)).ConfigureAwait(false);
        await transaction.CommitAsync(ct).ConfigureAwait(false);
        await RecordDepthAsync(connection, ct).ConfigureAwait(false);
        return rows.ToList();
    }

    private static async Task RecordDepthAsync(
        System.Data.Common.DbConnection connection,
        CancellationToken ct)
    {
        var depth = await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            """
            SELECT
                (SELECT count(*) FROM game_event_outbox WHERE status IN ('pending', 'sending'))
              + (SELECT count(*) FROM tenant_event_outbox WHERE status IN ('pending', 'sending'))
            """,
            cancellationToken: ct)).ConfigureAwait(false);
        BotFrameworkMetrics.SetOutboxDepth(depth);
    }

    public async Task MarkSentAsync(long id, CancellationToken ct)
    {
        await using var connection = await connections.OpenAsync(ct).ConfigureAwait(false);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE game_event_outbox
            SET status = 'sent',
                sent_at = now(),
                locked_until = NULL,
                last_error = NULL
            WHERE id = @id
            """,
            new { id },
            cancellationToken: ct)).ConfigureAwait(false);
    }

    public async Task MarkTenantSentAsync(long id, CancellationToken ct)
    {
        await using var connection = await connections.OpenAsync(ct).ConfigureAwait(false);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE tenant_event_outbox
            SET status = 'sent',
                sent_at = now(),
                locked_until = NULL,
                last_error = NULL
            WHERE id = @id
            """,
            new { id },
            cancellationToken: ct)).ConfigureAwait(false);
    }

    public async Task MarkFailedAsync(long id, string error, int attempts, CancellationToken ct)
    {
        var retrySeconds = Math.Min(300, Math.Pow(2, Math.Min(attempts, 8)));
        await using var connection = await connections.OpenAsync(ct).ConfigureAwait(false);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE game_event_outbox
            SET status = 'pending',
                next_attempt_at = now() + (@retrySeconds * interval '1 second'),
                locked_until = NULL,
                last_error = @error
            WHERE id = @id
            """,
            new
            {
                id,
                retrySeconds,
                error = error.Length <= 4000 ? error : error[..4000],
            },
            cancellationToken: ct)).ConfigureAwait(false);
    }

    public async Task MarkTenantFailedAsync(long id, string error, int attempts, CancellationToken ct)
    {
        var retrySeconds = Math.Min(300, Math.Pow(2, Math.Min(attempts, 8)));
        await using var connection = await connections.OpenAsync(ct).ConfigureAwait(false);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE tenant_event_outbox
            SET status = 'pending',
                next_attempt_at = now() + (@retrySeconds * interval '1 second'),
                locked_until = NULL,
                last_error = @error
            WHERE id = @id
            """,
            new
            {
                id,
                retrySeconds,
                error = error.Length <= 4000 ? error : error[..4000],
            },
            cancellationToken: ct)).ConfigureAwait(false);
    }
}
