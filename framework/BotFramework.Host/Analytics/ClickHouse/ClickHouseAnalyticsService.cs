// ─────────────────────────────────────────────────────────────────────────────
// ClickHouseAnalyticsService — the framework's IAnalyticsService, with a
// buffered ClickHouse sink.
//
// Schema (events_v2):
//   event_id    UUID          auto
//   event_type  String        "darts.throw_completed", "host.webhook", …
//   module      String        "darts" (split_part(event_type, '.', 1))
//   project     String        distribution id (ClickHouse__Project)
//   user_id     Int64         0 when not applicable
//   params      Map(String,String)   tag key/value pairs, stringified
//   payload     String        JSON; module-call payload OR full domain event
//   created_at  DateTime64(3)
//
// Inserts use ClickHouseBulkCopy — parameterized, no string-concat SQL.
//
// Two entry points:
//   • Track(moduleId, eventName, tags)  → legacy per-module analytics.
//   • TrackDomainEvent(IDomainEvent ev) → mirror of the event bus. Properties
//     are reflected into tags so Grafana can filter on params without
//     JSONExtract.
//
// Track is synchronous and non-blocking — all I/O runs on a PeriodicTimer-
// driven flush loop so game handlers never wait for ClickHouse.
// ─────────────────────────────────────────────────────────────────────────────

using System.Globalization;
using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ClickHouse.Client.ADO;
using ClickHouse.Client.Copy;
using Microsoft.Extensions.Options;

namespace BotFramework.Host.Analytics.ClickHouse;

public sealed partial class ClickHouseAnalyticsService(
    IOptions<ClickHouseOptions> options,
    ILogger<ClickHouseAnalyticsService> logger)
    : IAnalyticsService, IHostedService, IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };

    private readonly ClickHouseOptions _options = options.Value;
    private readonly Lock _lock = new();
    private readonly List<EventData> _buffer = [];
    private ClickHouseConnection? _connection;
    private PeriodicTimer? _flushTimer;
    private CancellationTokenSource? _loopCts;
    private Task? _flushLoop;
    private long _flushSucceeded;
    private long _flushFailed;
    private long _rowsFlushed;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            LogReporterDisabled();
            return;
        }

        if (string.IsNullOrWhiteSpace(_options.Host))
        {
            throw new InvalidOperationException(
                "ClickHouse:Enabled is true but ClickHouse:Host is not set.");
        }

        try
        {
            _connection = new ClickHouseConnection(BuildConnectionString(_options));
            await _connection.OpenAsync(cancellationToken);
            await CreateTableIfNotExistsAsync(cancellationToken);

            _loopCts = new CancellationTokenSource();
            _flushTimer = new PeriodicTimer(TimeSpan.FromMilliseconds(_options.FlushIntervalMs));
            _flushLoop = RunFlushLoopAsync(_loopCts.Token);
        }
        catch (Exception ex)
        {
            LogInitFailed(ex);
            if (_connection != null) await _connection.DisposeAsync();
            _connection = null;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _flushTimer?.Dispose();
        await _loopCts?.CancelAsync()!;
        if (_flushLoop != null)
        {
            try { await _flushLoop; }
            catch (OperationCanceledException)
            {
                // The flush loop exits through cancellation during service shutdown.
            }
        }

        await FlushBufferAsync();
        if (_connection != null) await _connection.DisposeAsync();
        _connection = null;
    }

    public void Track(string moduleId, string eventName, IReadOnlyDictionary<string, object?> tags)
    {
        if (!_options.Enabled) return;

        var eventType = $"{moduleId}.{eventName}";
        var enrichedTags = EnrichTags(tags);
        var userId = ExtractUserId(enrichedTags);
        var paramsMap = ToParamsMap(enrichedTags);
        var payload = JsonSerializer.Serialize(enrichedTags, JsonOpts);
        Enqueue(new EventData(
            Guid.NewGuid(), eventType, moduleId, userId, paramsMap, payload,
            DateTime.UtcNow, "", 0, "", 1, ContextValue("correlation_id"), ContextValue("causation_id"), false));
    }

    private static IReadOnlyDictionary<string, object?> EnrichTags(IReadOnlyDictionary<string, object?> tags)
    {
        var context = AnalyticsContextAccessor.Current;
        if (context is null || context.Count == 0) return tags;

        var enriched = new Dictionary<string, object?>(context, StringComparer.Ordinal);
        foreach (var (key, value) in tags)
            enriched[key] = value;
        return enriched;
    }

    public void TrackDomainEvent(IDomainEvent ev) => TrackDomainEvent(ev, EventAnalyticsEnvelopeAccessor.Current, isReplay: false);

    internal void TrackStoredDomainEvent(StoredEvent stored, IDomainEvent ev)
    {
        var correlationId = ReadPayloadString(stored.PayloadJson, "correlation_id");
        var envelope = new EventAnalyticsEnvelope(
            stored.StreamId,
            stored.Version,
            ev.GetType().FullName ?? ev.GetType().Name,
            SchemaVersion: 1,
            CorrelationId: correlationId,
            CausationId: ReadPayloadString(stored.PayloadJson, "causation_id", correlationId));
        TrackDomainEvent(ev, envelope, isReplay: true);
    }

    private void TrackDomainEvent(IDomainEvent ev, EventAnalyticsEnvelope? envelope, bool isReplay)
    {
        if (!_options.Enabled) return;

        var type = ev.GetType();
        var tags = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        long userId = 0;

        if (AnalyticsContextAccessor.Current is { } context)
        {
            foreach (var (key, value) in context)
                tags[key] = value;
        }
        if (envelope is not null)
        {
            tags["stream_id"] = envelope.StreamId;
            tags["stream_version"] = envelope.StreamVersion;
            tags["aggregate_type"] = envelope.AggregateType;
            tags["schema_version"] = envelope.SchemaVersion;
            tags["correlation_id"] = envelope.CorrelationId;
            tags["causation_id"] = envelope.CausationId;
            tags["is_replay"] = isReplay;
        }

        foreach (var p in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (string.Equals(p.Name, nameof(IDomainEvent.EventType), StringComparison.Ordinal)) continue;
            var v = p.GetValue(ev);
            tags[p.Name] = v;
            if (userId == 0 && v is long l &&
                (p.Name is "UserId" or "HostUserId" or "IssuedBy" or "RedeemedBy"))
            {
                userId = l;
            }
        }

        var module = SplitModule(ev.EventType);
        var paramsMap = ToParamsMap(tags);
        var payload = JsonSerializer.Serialize(ev, type, JsonOpts);
        var occurredAt = FromUnixMilliseconds(ev.OccurredAt);
        Enqueue(new EventData(
            envelope is null ? Guid.NewGuid() : DeterministicEventId(envelope.StreamId, envelope.StreamVersion),
            ev.EventType,
            module,
            userId,
            paramsMap,
            payload,
            DateTime.UtcNow,
            envelope?.StreamId ?? "",
            envelope?.StreamVersion ?? 0,
            envelope?.AggregateType ?? type.FullName ?? type.Name,
            envelope?.SchemaVersion ?? 1,
            envelope?.CorrelationId ?? ContextValue("correlation_id"),
            envelope?.CausationId ?? ContextValue("causation_id"),
            isReplay,
            occurredAt));
    }

    private static string ContextValue(string key) =>
        AnalyticsContextAccessor.Current?.TryGetValue(key, out var value) == true ? value?.ToString() ?? "" : "";

    private static DateTime FromUnixMilliseconds(long value)
    {
        try { return DateTimeOffset.FromUnixTimeMilliseconds(value).UtcDateTime; }
        catch (ArgumentOutOfRangeException) { return DateTime.UtcNow; }
    }

    private static Guid DeterministicEventId(string streamId, long streamVersion)
    {
        var input = Encoding.UTF8.GetBytes(string.Create(CultureInfo.InvariantCulture, $"{streamId}:{streamVersion}"));
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(input, hash);
        hash[6] = (byte)((hash[6] & 0x0F) | 0x50);
        hash[8] = (byte)((hash[8] & 0x3F) | 0x80);
        return new Guid(hash[..16]);
    }

    private static string ReadPayloadString(string payload, string property, string fallback = "")
    {
        try
        {
            using var document = JsonDocument.Parse(payload);
            foreach (var candidate in new[] { property, ToPascalCase(property) })
            {
                if (document.RootElement.TryGetProperty(candidate, out var value) && value.ValueKind == JsonValueKind.String)
                    return value.GetString() ?? fallback;
            }
        }
        catch (JsonException) { }
        return fallback;
    }

    private static string ToPascalCase(string value) => string.Concat(
        value.Split('_', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => char.ToUpperInvariant(part[0]) + part[1..]));

    private void Enqueue(EventData evt)
    {
        var shouldFlush = false;
        lock (_lock)
        {
            _buffer.Add(evt);
            if (_buffer.Count >= _options.BufferSize) shouldFlush = true;
        }
        if (shouldFlush) _ = Task.Run(FlushBufferAsync, _loopCts?.Token ?? CancellationToken.None);
    }

    private static long ExtractUserId(IReadOnlyDictionary<string, object?> tags)
    {
        foreach (var key in new[] { "user_id", "UserId", "host_user_id", "HostUserId" })
        {
            if (tags.TryGetValue(key, out var v) && v is not null)
            {
                if (v is long l) return l;
                if (v is int i) return i;
                if (long.TryParse(v.ToString(), System.Globalization.CultureInfo.InvariantCulture, out var parsed)) return parsed;
            }
        }
        return 0;
    }

    private static Dictionary<string, string> ToParamsMap(IReadOnlyDictionary<string, object?> tags)
    {
        var map = new Dictionary<string, string>(tags.Count, StringComparer.Ordinal);
        foreach (var (k, v) in tags)
        {
            if (v is null) continue;
            map[k] = v is string s ? s : v.ToString() ?? "";
        }
        return map;
    }

    private static string SplitModule(string eventType)
    {
        var dot = eventType.IndexOf('.', StringComparison.Ordinal);
        return dot < 0 ? eventType : eventType[..dot];
    }

    private async Task RunFlushLoopAsync(CancellationToken ct)
    {
        while (_flushTimer != null && await _flushTimer.WaitForNextTickAsync(ct))
        {
            await FlushBufferAsync();
        }
    }

    private async Task FlushBufferAsync()
    {
        var stopwatch = Stopwatch.StartNew();
        if (_connection is null) return;
        List<EventData> eventsToSend;
        lock (_lock)
        {
            if (_buffer.Count == 0) return;
            eventsToSend = [.. _buffer];
            _buffer.Clear();
        }

        try
        {
            var mainEvents = eventsToSend.Where(e => !e.IsReplay).ToList();
            var esEvents = eventsToSend.Where(e => !string.IsNullOrEmpty(e.StreamId)).ToList();
            await WriteEventsAsync($"{_options.Database}.{_options.Table}", mainEvents);
            await WriteEventsAsync($"{_options.Database}.{_options.Table}_es", esEvents);

            var succeeded = Interlocked.Increment(ref _flushSucceeded);
            var rowsFlushed = Interlocked.Add(ref _rowsFlushed, eventsToSend.Count);
            var failed = Interlocked.Read(ref _flushFailed);
            stopwatch.Stop();

            var tags = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["batch_size"] = eventsToSend.Count,
                ["flush_duration_ms"] = stopwatch.Elapsed.TotalMilliseconds,
                ["flush_succeeded_total"] = succeeded,
                ["flush_failed_total"] = failed,
                ["rows_flushed_total"] = rowsFlushed,
                ["buffer_depth"] = BufferCount(),
            };
            var paramsMap = ToParamsMap(tags);
            lock (_lock)
            {
                _buffer.Add(new EventData(
                    Guid.NewGuid(),
                    "meta_analytics.clickhouse_writer_health",
                    "meta_analytics",
                    0,
                    paramsMap,
                    JsonSerializer.Serialize(tags, JsonOpts),
                    DateTime.UtcNow,
                    "", 0, "", 1, "", "", false));
            }
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref _flushFailed);
            LogFlushFailed(ex);
            lock (_lock) _buffer.InsertRange(0, eventsToSend);
        }
    }

    private async Task WriteEventsAsync(string destination, IReadOnlyCollection<EventData> events)
    {
        if (_connection is null || events.Count == 0) return;

        using var bulk = new ClickHouseBulkCopy(_connection)
        {
            DestinationTableName = destination,
            ColumnNames = ["event_id", "event_type", "module", "project", "user_id", "params", "payload", "created_at", "stream_id", "stream_version", "aggregate_type", "schema_version", "correlation_id", "causation_id", "is_replay", "occurred_at"],
            BatchSize = Math.Max(_options.BufferSize, 100),
        };
        await bulk.InitAsync();
        var rows = events.Select(e => new object[]
        {
            e.EventId, e.EventType, e.Module, _options.Project, e.UserId, e.Params, e.Payload, e.CreatedAt,
            e.StreamId, e.StreamVersion, e.AggregateType, e.SchemaVersion, e.CorrelationId, e.CausationId,
            e.IsReplay ? (byte)1 : (byte)0, e.OccurredAt,
        });
        await bulk.WriteToServerAsync(rows, _loopCts?.Token ?? CancellationToken.None);
    }

    internal async Task<bool> FlushNowAsync()
    {
        var failedBefore = Interlocked.Read(ref _flushFailed);
        var rowsBefore = Interlocked.Read(ref _rowsFlushed);
        await FlushBufferAsync();
        return Interlocked.Read(ref _flushFailed) == failedBefore && Interlocked.Read(ref _rowsFlushed) > rowsBefore;
    }

    private int BufferCount()
    {
        lock (_lock) return _buffer.Count;
    }

    internal IReadOnlyList<AnalyticsEventSnapshot> SnapshotBufferedEvents()
    {
        lock (_lock)
        {
            return _buffer.Select(e => new AnalyticsEventSnapshot(
                e.EventId, e.EventType, e.Module, e.UserId,
                new Dictionary<string, string>(e.Params, StringComparer.Ordinal),
                e.StreamId, e.StreamVersion, e.AggregateType, e.SchemaVersion,
                e.CorrelationId, e.CausationId, e.IsReplay, e.OccurredAt)).ToList();
        }
    }

    private async Task CreateTableIfNotExistsAsync(CancellationToken ct)
    {
        if (_connection is null) return;

        await using (var dbCmd = _connection.CreateCommand())
        {
            dbCmd.CommandText = $"CREATE DATABASE IF NOT EXISTS {_options.Database}";
            await dbCmd.ExecuteNonQueryAsync(ct);
        }

        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = $$"""
            CREATE TABLE IF NOT EXISTS {{_options.Database}}.{{_options.Table}} (
                event_id    UUID               DEFAULT generateUUIDv4(),
                event_type  LowCardinality(String),
                module      LowCardinality(String),
                project     LowCardinality(String),
                user_id     Int64,
                params      Map(String, String),
                payload     String,
                created_at  DateTime64(3)      DEFAULT now64(3),
                stream_id   String             DEFAULT '',
                stream_version Int64           DEFAULT 0,
                aggregate_type LowCardinality(String) DEFAULT '',
                schema_version UInt16          DEFAULT 1,
                correlation_id String          DEFAULT '',
                causation_id String             DEFAULT '',
                is_replay UInt8                 DEFAULT 0,
                occurred_at DateTime64(3)       DEFAULT created_at
            ) ENGINE = MergeTree()
            ORDER BY (project, module, event_type, created_at)
            SETTINGS index_granularity = 8192
            """;
        await cmd.ExecuteNonQueryAsync(ct);

        await using var alter = _connection.CreateCommand();
        alter.CommandText = $$"""
            ALTER TABLE {{_options.Database}}.{{_options.Table}}
                ADD COLUMN IF NOT EXISTS stream_id String DEFAULT '',
                ADD COLUMN IF NOT EXISTS stream_version Int64 DEFAULT 0,
                ADD COLUMN IF NOT EXISTS aggregate_type LowCardinality(String) DEFAULT '',
                ADD COLUMN IF NOT EXISTS schema_version UInt16 DEFAULT 1,
                ADD COLUMN IF NOT EXISTS correlation_id String DEFAULT '',
                ADD COLUMN IF NOT EXISTS causation_id String DEFAULT '',
                ADD COLUMN IF NOT EXISTS is_replay UInt8 DEFAULT 0,
                ADD COLUMN IF NOT EXISTS occurred_at DateTime64(3) DEFAULT created_at
            """;
        await alter.ExecuteNonQueryAsync(ct);

        await using var esTable = _connection.CreateCommand();
        esTable.CommandText = $$"""
            CREATE TABLE IF NOT EXISTS {{_options.Database}}.{{_options.Table}}_es (
                event_id UUID,
                event_type LowCardinality(String),
                module LowCardinality(String),
                project LowCardinality(String),
                user_id Int64,
                params Map(String, String),
                payload String,
                created_at DateTime64(3),
                stream_id String,
                stream_version Int64,
                aggregate_type LowCardinality(String),
                schema_version UInt16,
                correlation_id String,
                causation_id String,
                is_replay UInt8,
                occurred_at DateTime64(3)
            ) ENGINE = ReplacingMergeTree(created_at)
            PARTITION BY toYYYYMM(occurred_at)
            ORDER BY (project, event_id)
            SETTINGS index_granularity = 8192
            """;
        await esTable.ExecuteNonQueryAsync(ct);
        LogTableReady($"{_options.Database}.{_options.Table}");
    }

    private static string BuildConnectionString(ClickHouseOptions o)
    {
        var parts = new List<string>();
        if (Uri.TryCreate(o.Host, UriKind.Absolute, out var uri))
        {
            parts.Add($"Host={uri.Host}");
            if (!uri.IsDefaultPort) parts.Add(string.Create(CultureInfo.InvariantCulture, $"Port={uri.Port}"));
            parts.Add($"Protocol={uri.Scheme}");
        }
        else
        {
            parts.Add($"Host={o.Host}");
        }
        parts.Add($"Username={o.User}");
        parts.Add($"Password={o.Password}");
        parts.Add($"Database={o.Database}");
        return string.Join(';', parts);
    }

    public async ValueTask DisposeAsync()
    {
        _flushTimer?.Dispose();
        _loopCts?.Dispose();
        if (_connection != null) await _connection.DisposeAsync();
    }

    private sealed record EventData(
        Guid EventId,
        string EventType,
        string Module,
        long UserId,
        Dictionary<string, string> Params,
        string Payload,
        DateTime CreatedAt,
        string StreamId,
        long StreamVersion,
        string AggregateType,
        int SchemaVersion,
        string CorrelationId,
        string CausationId,
        bool IsReplay,
        DateTime? OriginalOccurredAt = null)
    {
        public DateTime OccurredAt => OriginalOccurredAt ?? CreatedAt;
    }

    [LoggerMessage(LogLevel.Information, "clickhouse.disabled")]
    partial void LogReporterDisabled();

    [LoggerMessage(LogLevel.Error, "clickhouse.init_failed analytics=disabled")]
    partial void LogInitFailed(Exception exception);

    [LoggerMessage(LogLevel.Error, "clickhouse.flush_failed buffer_restored")]
    partial void LogFlushFailed(Exception exception);

    [LoggerMessage(LogLevel.Information, "clickhouse.table_ready name={Table}")]
    partial void LogTableReady(string table);
}
