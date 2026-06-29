// ─────────────────────────────────────────────────────────────────────────────
// EventDispatcher — fans persisted aggregate events into read-model projections,
// cross-module subscribers and framework analytics.
//
// IEventStore only stores events. EventSourcedRepository<T> owns the aggregate
// lifecycle and invokes this dispatcher after a successful append. That means
// every event-sourced aggregate registered with
// RegisterAggregate<T>(PersistenceStrategy.EventSourced) automatically flows
// through projections, EventLogSubscriber, ClickHouseEventMirror, module
// subscriptions and analytics.
//
// Current delivery semantics are post-commit: if dispatch fails, the original
// event append stays committed and the failure is logged by the repository.
// Projection rebuild/replay is the recovery mechanism. A future same-transaction
// unit-of-work can pass a provider-specific transaction object through
// ProjectionContext.Transaction without changing module contracts.
// ─────────────────────────────────────────────────────────────────────────────


using BotFramework.Host.Analytics;

namespace BotFramework.Host.Events.Dispatch;

public sealed class EventDispatcher(
    IEnumerable<IProjection> projections,
    IDomainEventBus bus)
{
    private readonly Dictionary<string, List<IProjection>> _byEventType = BuildIndex(projections);

    public async Task DispatchAsync(
        string streamId,
        long streamVersion,
        IDomainEvent ev,
        object? transaction,
        CancellationToken ct)
    {
        // 1. Projections first — they maintain read models other subscribers
        //    may want to query. Transaction is null in the current post-commit
        //    mode and may be provider-specific in a future unit-of-work mode.
        if (_byEventType.TryGetValue(ev.EventType, out var subscribers))
        {
            var ctx = new ProjectionContext(streamId, streamVersion, ev.OccurredAt, transaction);
            foreach (var proj in subscribers)
                await proj.ApplyAsync(ev, ctx, ct);
        }

        // 2. Cross-module subscribers. The ClickHouse wildcard subscriber reads
        //    this envelope so one full-payload analytics row carries stable ES
        //    identity; the old second metadata-only Track call caused duplicates.
        var previousEnvelope = EventAnalyticsEnvelopeAccessor.Current;
        var analyticsContext = AnalyticsContextAccessor.Current;
        var correlationId = GetContextValue(analyticsContext, "correlation_id");
        EventAnalyticsEnvelopeAccessor.Current = new EventAnalyticsEnvelope(
            streamId,
            streamVersion,
            AggregateType: ev.GetType().FullName ?? ev.GetType().Name,
            SchemaVersion: 1,
            CorrelationId: correlationId,
            CausationId: GetContextValue(analyticsContext, "causation_id", correlationId));
        try
        {
            await bus.PublishAsync(ev, ct);
        }
        finally
        {
            EventAnalyticsEnvelopeAccessor.Current = previousEnvelope;
        }
    }

    private static string GetContextValue(
        IReadOnlyDictionary<string, object?>? context,
        string key,
        string fallback = "") =>
        context?.TryGetValue(key, out var value) == true ? value?.ToString() ?? fallback : fallback;

    private static Dictionary<string, List<IProjection>> BuildIndex(IEnumerable<IProjection> projections)
    {
        var index = new Dictionary<string, List<IProjection>>(StringComparer.Ordinal);
        foreach (var proj in projections)
        {
            foreach (var type in proj.SubscribedEventTypes)
        {
            if (!index.TryGetValue(type, out var list))
                index[type] = list = [];
            list.Add(proj);
        }
        }

        return index;
    }
}
