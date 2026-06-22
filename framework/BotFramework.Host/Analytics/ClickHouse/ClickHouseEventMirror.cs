using BotFramework.Sdk;

namespace BotFramework.Host.Analytics.ClickHouse;

internal sealed class ClickHouseEventMirror(ClickHouseAnalyticsService analytics) : IDomainEventSubscriber
{
    public Task HandleAsync(IDomainEvent ev, CancellationToken ct)
    {
        analytics.TrackDomainEvent(ev);
        return Task.CompletedTask;
    }
}
