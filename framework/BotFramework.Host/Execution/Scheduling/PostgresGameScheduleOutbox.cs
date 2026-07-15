using Dapper;

namespace BotFramework.Host.Execution;

internal sealed class PostgresGameScheduleOutbox(INpgsqlConnectionFactory connections)
{
    public async Task<IReadOnlyList<GameScheduleOutboxItem>> ClaimAsync(
        int limit,
        TimeSpan lease,
        CancellationToken ct,
        IReadOnlySet<string>? ownedGameIds = null)
    {
        await using var connection = await connections.OpenAsync(ct).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(ct).ConfigureAwait(false);
        var rows = await connection.QueryAsync<GameScheduleOutboxItem>(new CommandDefinition(
            """
            WITH due AS (
                SELECT candidate.id
                FROM game_schedule_outbox AS candidate
                WHERE ((candidate.status = 'pending' AND candidate.next_attempt_at <= now())
                    OR (candidate.status = 'sending' AND candidate.locked_until <= now()))
                  AND (@claimAll OR candidate.game_id = ANY(@gameIds))
                  AND NOT EXISTS (
                      SELECT 1
                      FROM game_schedule_outbox AS earlier
                      WHERE earlier.schedule_id = candidate.schedule_id
                        AND earlier.id < candidate.id
                        AND earlier.status <> 'sent')
                ORDER BY candidate.next_attempt_at, candidate.id
                LIMIT @limit
                FOR UPDATE OF candidate SKIP LOCKED
            )
            UPDATE game_schedule_outbox AS outbox
            SET status = 'sending',
                attempts = outbox.attempts + 1,
                locked_until = now() + (@leaseMs * interval '1 millisecond')
            FROM due
            WHERE outbox.id = due.id
            RETURNING outbox.id AS Id,
                      outbox.effect_kind AS EffectKind,
                      outbox.schedule_id AS ScheduleId,
                      outbox.job_key AS JobKey,
                      CAST(EXTRACT(EPOCH FROM outbox.due_at) * 1000 AS BIGINT) AS DueAtUnixMilliseconds,
                      outbox.data::text AS Data,
                      outbox.attempts AS Attempts
            """,
            new
            {
                limit = Math.Clamp(limit, 1, 100),
                leaseMs = Math.Max(1, lease.TotalMilliseconds),
                claimAll = ownedGameIds is null,
                gameIds = ownedGameIds?.ToArray() ?? [],
            },
            transaction,
            cancellationToken: ct)).ConfigureAwait(false);
        await transaction.CommitAsync(ct).ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task<IReadOnlyList<TenantGameScheduleOutboxItem>> ClaimTenantAsync(
        int limit,
        TimeSpan lease,
        CancellationToken ct,
        IReadOnlySet<string>? ownedGameIds = null)
    {
        await using var connection = await connections.OpenAsync(ct).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(ct).ConfigureAwait(false);
        var rows = await connection.QueryAsync<TenantGameScheduleOutboxItem>(new CommandDefinition(
            """
            WITH due AS (
                SELECT candidate.id
                FROM tenant_schedule_outbox AS candidate
                WHERE ((candidate.status = 'pending' AND candidate.next_attempt_at <= now())
                    OR (candidate.status = 'sending' AND candidate.locked_until <= now()))
                  AND (@claimAll OR candidate.game_id = ANY(@gameIds))
                  AND NOT EXISTS (
                      SELECT 1
                      FROM tenant_schedule_outbox AS earlier
                      WHERE earlier.tenant_key = candidate.tenant_key
                        AND earlier.scope_key = candidate.scope_key
                        AND earlier.schedule_id = candidate.schedule_id
                        AND earlier.id < candidate.id
                        AND earlier.status <> 'sent')
                ORDER BY candidate.next_attempt_at, candidate.id
                LIMIT @limit
                FOR UPDATE OF candidate SKIP LOCKED
            )
            UPDATE tenant_schedule_outbox AS outbox
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
                      outbox.effect_kind AS EffectKind,
                      outbox.schedule_id AS ScheduleId,
                      outbox.job_key AS JobKey,
                      CAST(EXTRACT(EPOCH FROM outbox.due_at) * 1000 AS BIGINT) AS DueAtUnixMilliseconds,
                      outbox.data::text AS Data,
                      outbox.attempts AS Attempts
            """,
            new
            {
                limit = Math.Clamp(limit, 1, 100),
                leaseMs = Math.Max(1, lease.TotalMilliseconds),
                claimAll = ownedGameIds is null,
                gameIds = ownedGameIds?.ToArray() ?? [],
            },
            transaction,
            cancellationToken: ct)).ConfigureAwait(false);
        await transaction.CommitAsync(ct).ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task MarkSentAsync(long id, CancellationToken ct)
    {
        await using var connection = await connections.OpenAsync(ct).ConfigureAwait(false);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE game_schedule_outbox
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
            UPDATE tenant_schedule_outbox
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
            UPDATE game_schedule_outbox
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
            UPDATE tenant_schedule_outbox
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
