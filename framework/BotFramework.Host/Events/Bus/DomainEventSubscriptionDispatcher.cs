namespace BotFramework.Host.Events.Bus;

public sealed partial class DomainEventSubscriptionDispatcher
{
    private readonly ILogger _logger;
    private readonly List<Subscription> _subscriptions = [];

    public DomainEventSubscriptionDispatcher(ILogger<DomainEventSubscriptionDispatcher> logger) => _logger = logger;
    internal DomainEventSubscriptionDispatcher(ILogger logger) => _logger = logger;

    public void Subscribe(string pattern, IDomainEventSubscriber subscriber) =>
        _subscriptions.Add(new(pattern, subscriber));

    public async Task DispatchAsync(IDomainEvent domainEvent, CancellationToken ct)
    {
        foreach (var subscription in _subscriptions.Where(subscription => Matches(subscription.Pattern, domainEvent.EventType)))
        {
            try { await subscription.Subscriber.HandleAsync(domainEvent, ct); }
            catch (Exception exception) { LogSubscriberFailed(exception, domainEvent.EventType, subscription.Subscriber.GetType().Name); }
        }
    }

    private static bool Matches(string pattern, string eventType)
    {
        if (string.Equals(pattern, "*", StringComparison.Ordinal) || string.Equals(pattern, eventType, StringComparison.Ordinal)) return true;
        var patternDot = pattern.IndexOf('.', StringComparison.Ordinal);
        var eventDot = eventType.IndexOf('.', StringComparison.Ordinal);
        if (patternDot < 0 || eventDot < 0) return false;
        var patternModule = pattern[..patternDot];
        var patternAction = pattern[(patternDot + 1)..];
        var eventModule = eventType[..eventDot];
        var eventAction = eventType[(eventDot + 1)..];
        return (patternModule == "*" || string.Equals(patternModule, eventModule, StringComparison.Ordinal))
            && (patternAction == "*" || string.Equals(patternAction, eventAction, StringComparison.Ordinal));
    }

    [LoggerMessage(LogLevel.Error, "event_bus.subscriber_failed event={EventType} subscriber={Subscriber}")]
    private partial void LogSubscriberFailed(Exception exception, string eventType, string subscriber);

    private sealed record Subscription(string Pattern, IDomainEventSubscriber Subscriber);
}
