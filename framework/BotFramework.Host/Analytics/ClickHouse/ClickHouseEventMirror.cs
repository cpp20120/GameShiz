using BotFramework.Sdk;

namespace BotFramework.Host.Analytics;

internal sealed class ClickHouseEventMirror(ClickHouseAnalyticsService analytics) : IDomainEventSubscriber
{
    public Task HandleAsync(IDomainEvent ev, CancellationToken ct)
    {
        analytics.TrackDomainEvent(ev);
        return Task.CompletedTask;
    }
}
