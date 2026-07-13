using BotFramework.Host.Analytics;
using BotFramework.Host.Analytics.ClickHouse;
using BotFramework.Host.Events;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CasinoShiz.Tests;

public sealed class ClickHouseAnalyticsServiceTests
{
    [Fact]
    public async Task StopAsync_WhenDisabled_DoesNotThrow()
    {
        var service = new ClickHouseAnalyticsService(
            Options.Create(new ClickHouseOptions { Enabled = false }),
            NullLogger<ClickHouseAnalyticsService>.Instance);

        await service.StartAsync(CancellationToken.None);
        await service.StopAsync(CancellationToken.None);
        Assert.Empty(service.SnapshotBufferedEvents());
    }

    [Fact]
    public void Track_EnrichesAndNormalizesBufferedEvent()
    {
        var service = CreateService();
        AnalyticsContextAccessor.Current = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["correlation_id"] = "telegram:42",
            ["session_id"] = "session-1",
            ["chat_id"] = 99L,
        };

        try
        {
            service.Track("leaderboard", "viewed", new Dictionary<string, object?>
            {
                ["user_id"] = 7L,
                ["outcome"] = "success",
            });
        }
        finally
        {
            AnalyticsContextAccessor.Current = null;
        }

        var row = Assert.Single(service.SnapshotBufferedEvents());
        Assert.Equal("leaderboard.viewed", row.EventType);
        Assert.Equal("leaderboard", row.Module);
        Assert.Equal(7, row.UserId);
        Assert.Equal("telegram:42", row.CorrelationId);
        Assert.Equal("session-1", row.Params["session_id"]);
        Assert.Equal("99", row.Params["chat_id"]);
    }

    [Fact]
    public void StoredDomainEvent_IsReplayOnlyAndHasDeterministicIdentity()
    {
        var service = CreateService();
        var occurredAt = DateTimeOffset.UtcNow.AddHours(-1).ToUnixTimeMilliseconds();
        var ev = new AnalyticsTestEvent(occurredAt, 17);
        var stored = new StoredEvent(
            "stream-1", 3, ev.EventType,
            "{\"correlation_id\":\"corr-1\",\"causation_id\":\"cause-1\"}", occurredAt);

        service.TrackStoredDomainEvent(stored, ev);
        service.TrackStoredDomainEvent(stored, ev);

        var rows = service.SnapshotBufferedEvents();
        Assert.Equal(2, rows.Count);
        Assert.All(rows, row =>
        {
            Assert.True(row.IsReplay);
            Assert.Equal("stream-1", row.StreamId);
            Assert.Equal(3, row.StreamVersion);
            Assert.Equal("corr-1", row.CorrelationId);
            Assert.Equal("cause-1", row.CausationId);
            Assert.Equal(17, row.UserId);
        });
        Assert.Equal(rows[0].EventId, rows[1].EventId);
    }

    private static ClickHouseAnalyticsService CreateService() => new(
        Options.Create(new ClickHouseOptions
        {
            Enabled = true,
            Host = "http://localhost:8123",
            BufferSize = 10_000,
        }),
        NullLogger<ClickHouseAnalyticsService>.Instance);

    private sealed record AnalyticsTestEvent(long OccurredAt, long UserId) : IDomainEvent
    {
        public string EventType => "analytics_test.completed";
    }
}
