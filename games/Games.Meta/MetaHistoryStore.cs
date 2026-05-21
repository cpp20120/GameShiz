using System.Text.Json;
using BotFramework.Host;
using Dapper;

namespace Games.Meta;

public interface IMetaHistoryStore
{
    Task AppendAsync(string eventType, string aggregateType, string aggregateId, long? seasonId, long? chatId, long? userId, object payload, CancellationToken ct);
    Task<IReadOnlyList<MetaHistoryEvent>> ListAsync(string? eventType, string? aggregateType, string? aggregateId, long? chatId, long? userId, int limit, CancellationToken ct);
    Task<MetaHistoryStats> GetStatsAsync(CancellationToken ct);
}

public sealed class MetaHistoryStore(INpgsqlConnectionFactory connections) : IMetaHistoryStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task AppendAsync(string eventType, string aggregateType, string aggregateId, long? seasonId, long? chatId, long? userId, object payload, CancellationToken ct)
    {
        await using var conn = await connections.OpenAsync(ct);
        await EnsureSchemaAsync(conn, ct);
        const string sql = """
            INSERT INTO meta_event_log (event_type, aggregate_type, aggregate_id, season_id, chat_id, user_id, payload)
            VALUES (@eventType, @aggregateType, @aggregateId, @seasonId, @chatId, @userId, CAST(@payloadJson AS jsonb))
            """;
        await conn.ExecuteAsync(new CommandDefinition(sql, new
        {
            eventType,
            aggregateType,
            aggregateId,
            seasonId,
            chatId,
            userId,
            payloadJson = JsonSerializer.Serialize(payload, JsonOptions),
        }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<MetaHistoryEvent>> ListAsync(string? eventType, string? aggregateType, string? aggregateId, long? chatId, long? userId, int limit, CancellationToken ct)
    {
        await using var conn = await connections.OpenAsync(ct);
        await EnsureSchemaAsync(conn, ct);
        const string sql = """
            SELECT id AS Id,
                   event_type AS EventType,
                   aggregate_type AS AggregateType,
                   aggregate_id AS AggregateId,
                   season_id AS SeasonId,
                   chat_id AS ChatId,
                   user_id AS UserId,
                   payload::text AS PayloadJson,
                   occurred_at AS OccurredAt
            FROM meta_event_log
            WHERE (@eventType IS NULL OR event_type = @eventType)
              AND (@aggregateType IS NULL OR aggregate_type = @aggregateType)
              AND (@aggregateId IS NULL OR aggregate_id = @aggregateId)
              AND (@chatId IS NULL OR chat_id = @chatId)
              AND (@userId IS NULL OR user_id = @userId)
            ORDER BY id DESC
            LIMIT @limit
            """;
        var rows = await conn.QueryAsync<MetaHistoryEvent>(new CommandDefinition(sql, new
        {
            eventType = BlankToNull(eventType),
            aggregateType = BlankToNull(aggregateType),
            aggregateId = BlankToNull(aggregateId),
            chatId,
            userId,
            limit = Math.Clamp(limit, 1, 1000),
        }, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<MetaHistoryStats> GetStatsAsync(CancellationToken ct)
    {
        await using var conn = await connections.OpenAsync(ct);
        await EnsureSchemaAsync(conn, ct);
        const string sql = """
            SELECT count(*)::bigint AS TotalEvents,
                   count(*) FILTER (WHERE event_type = 'game.completed')::bigint AS GameCompletedEvents,
                   count(*) FILTER (WHERE aggregate_type = 'tournament')::bigint AS TournamentEvents,
                   min(occurred_at) AS FirstEventAt,
                   max(occurred_at) AS LastEventAt
            FROM meta_event_log
            """;
        return await conn.QuerySingleAsync<MetaHistoryStats>(new CommandDefinition(sql, cancellationToken: ct));
    }

    private static async Task EnsureSchemaAsync(System.Data.Common.DbConnection conn, CancellationToken ct)
    {
        const string sql = """
            CREATE TABLE IF NOT EXISTS meta_event_log (
                id              BIGSERIAL    PRIMARY KEY,
                event_type      TEXT         NOT NULL,
                aggregate_type  TEXT         NOT NULL,
                aggregate_id    TEXT         NOT NULL,
                season_id       BIGINT       NULL,
                chat_id         BIGINT       NULL,
                user_id         BIGINT       NULL,
                payload         JSONB        NOT NULL DEFAULT '{}'::jsonb,
                occurred_at     TIMESTAMPTZ  NOT NULL DEFAULT now()
            );

            CREATE INDEX IF NOT EXISTS ix_meta_event_log_type_id
                ON meta_event_log (event_type, id DESC);

            CREATE INDEX IF NOT EXISTS ix_meta_event_log_aggregate
                ON meta_event_log (aggregate_type, aggregate_id, id DESC);

            CREATE INDEX IF NOT EXISTS ix_meta_event_log_chat_user
                ON meta_event_log (chat_id, user_id, id DESC);
            """;
        await conn.ExecuteAsync(new CommandDefinition(sql, cancellationToken: ct));
    }

    private static string? BlankToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed record MetaHistoryEvent(
    long Id,
    string EventType,
    string AggregateType,
    string AggregateId,
    long? SeasonId,
    long? ChatId,
    long? UserId,
    string PayloadJson,
    DateTimeOffset OccurredAt);

public sealed record MetaHistoryStats(
    long TotalEvents,
    long GameCompletedEvents,
    long TournamentEvents,
    DateTimeOffset? FirstEventAt,
    DateTimeOffset? LastEventAt);
