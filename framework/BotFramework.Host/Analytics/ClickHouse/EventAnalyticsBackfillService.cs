using Dapper;
using Microsoft.Extensions.Options;

namespace BotFramework.Host.Analytics.ClickHouse;

internal sealed partial class EventAnalyticsBackfillService(
    INpgsqlConnectionFactory connections,
    IEventSerializer serializer,
    ClickHouseAnalyticsService analytics,
    IOptions<ClickHouseOptions> options,
    ILogger<EventAnalyticsBackfillService> logger) : BackgroundService
{
    private const int BatchSize = 500;
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled) return;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                while (await ProcessBatchAsync(stoppingToken)) { }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                LogBackfillFailed(ex);
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    private async Task<bool> ProcessBatchAsync(CancellationToken ct)
    {
        await using var conn = await connections.OpenAsync(ct);
        var checkpoint = await conn.QuerySingleAsync<long>(new CommandDefinition(
            "SELECT last_event_id FROM event_analytics_checkpoint WHERE id = 1",
            cancellationToken: ct));

        const string sql = """
            SELECT id AS "Id",
                   stream_id AS "StreamId",
                   version AS "Version",
                   event_type AS "EventType",
                   payload::text AS "PayloadText",
                   (extract(epoch from occurred_at) * 1000)::bigint AS "OccurredAtMs"
            FROM module_events
            WHERE id > @checkpoint
            ORDER BY id
            LIMIT @batchSize
            """;
        var rows = (await conn.QueryAsync<EventBackfillRow>(new CommandDefinition(
            sql, new { checkpoint, batchSize = BatchSize }, cancellationToken: ct))).AsList();
        if (rows.Count == 0) return false;

        var tracked = 0;
        foreach (var row in rows)
        {
            var ev = serializer.Deserialize(row.EventType, row.PayloadText);
            if (ev is null)
            {
                LogUnknownEvent(row.Id, row.EventType);
                continue;
            }

            analytics.TrackStoredDomainEvent(new StoredEvent(
                row.StreamId, row.Version, row.EventType, row.PayloadText, row.OccurredAtMs), ev);
            tracked++;
        }

        if (tracked > 0 && !await analytics.FlushNowAsync())
            throw new InvalidOperationException("ClickHouse event backfill flush failed; checkpoint was not advanced.");

        var lastId = rows[^1].Id;
        await conn.ExecuteAsync(new CommandDefinition("""
            UPDATE event_analytics_checkpoint
            SET last_event_id = @lastId, updated_at = now()
            WHERE id = 1
            """, new { lastId }, cancellationToken: ct));

        analytics.Track("meta_analytics", "event_backfill_health", new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["rows_read"] = rows.Count,
            ["rows_tracked"] = tracked,
            ["last_event_id"] = lastId,
            ["outcome"] = "ok",
        });
        LogBatchCompleted(rows.Count, tracked, lastId);
        return rows.Count == BatchSize;
    }

    private sealed record EventBackfillRow(
        long Id,
        string StreamId,
        long Version,
        string EventType,
        string PayloadText,
        long OccurredAtMs);

    [LoggerMessage(LogLevel.Information, "event_analytics.backfill batch={Rows} tracked={Tracked} last_event_id={LastEventId}")]
    partial void LogBatchCompleted(int rows, int tracked, long lastEventId);

    [LoggerMessage(LogLevel.Warning, "event_analytics.backfill unknown_event id={EventId} event_type={EventType}")]
    partial void LogUnknownEvent(long eventId, string eventType);

    [LoggerMessage(LogLevel.Error, "event_analytics.backfill failed retry_in_minutes=5")]
    partial void LogBackfillFailed(Exception exception);
}
