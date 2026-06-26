// ─────────────────────────────────────────────────────────────────────────────
// InProcessEventBus — synchronous, same-process domain event dispatcher.
//
// Subscriptions are matched by pattern at publish time. The pattern grammar
// is intentionally tiny:
//
//   "sh.game_ended"   exact match (module + action)
//   "sh.*"            every event from module "sh"
//   "*.game_ended"    game_ended from any module
//   "*"               everything
//
// Matching happens on publish; subscription is O(patterns). For a small
// framework that's fine. If pattern count ever grows past a hundred, the
// fix is a trie indexed by "module" then "action", not a rewrite.
//
// Subscribers run sequentially in registration order. Each subscriber failure
// is caught and logged — one bad subscriber never kills others or the caller.
// ─────────────────────────────────────────────────────────────────────────────


namespace BotFramework.Host.Events.Bus;

public sealed partial class InProcessEventBus(ILogger<InProcessEventBus> logger) : IDomainEventBus
{
    private readonly List<Subscription> _subs = [];

    public void Subscribe(string eventTypePattern, IDomainEventSubscriber subscriber) =>
        _subs.Add(new Subscription(eventTypePattern, subscriber));

    public async Task PublishAsync(IDomainEvent ev, CancellationToken ct)
    {
        foreach (var sub in _subs)
        {
            if (!Matches(sub.Pattern, ev.EventType)) continue;
            try
            {
                await sub.Subscriber.HandleAsync(ev, ct);
            }
            catch (Exception ex)
            {
                LogSubscriberFailed(ex, ev.EventType, sub.Subscriber.GetType().Name);
            }
        }
    }

    private static bool Matches(string pattern, string eventType)
    {
        if (string.Equals(pattern, "*", StringComparison.Ordinal)) return true;
        if (string.Equals(pattern, eventType, StringComparison.Ordinal)) return true;

        var dot = pattern.IndexOf('.', StringComparison.Ordinal);
        if (dot < 0) return false;

        var (patMod, patAction) = (pattern[..dot], pattern[(dot + 1)..]);

        var evDot = eventType.IndexOf('.', StringComparison.Ordinal);
        if (evDot < 0) return false;

        var (evMod, evAction) = (eventType[..evDot], eventType[(evDot + 1)..]);

        return (string.Equals(patMod, "*", StringComparison.Ordinal) || string.Equals(patMod, evMod, StringComparison.Ordinal)) && (string.Equals(patAction, "*", StringComparison.Ordinal) || string.Equals(patAction, evAction, StringComparison.Ordinal));
    }

    [LoggerMessage(LogLevel.Error, "event_bus.subscriber_failed event={EventType} subscriber={Subscriber}")]
    partial void LogSubscriberFailed(Exception ex, string eventType, string subscriber);

    private sealed record Subscription(string Pattern, IDomainEventSubscriber Subscriber);
}
