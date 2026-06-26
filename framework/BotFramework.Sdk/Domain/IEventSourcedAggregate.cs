// ─────────────────────────────────────────────────────────────────────────────
// Aggregate + persistence abstractions.
//
// Application services depend on IRepository<T>. The concrete repo might be
// EF-backed (classical) or event-store-backed (event-sourced) — services don't
// care. That's the whole point: persistence choice is a module decision, not a
// service decision.
// ─────────────────────────────────────────────────────────────────────────────

namespace BotFramework.Sdk.Domain;
/// <summary>
/// Opt-in extension for aggregates that want event sourcing. Classical
/// aggregates simply implement IAggregateRoot and are persisted as rows.
/// </summary>
public interface IEventSourcedAggregate : IAggregateRoot
{
    /// <summary>
    /// Monotonic version. Incremented each time an event is applied. Used as
    /// the optimistic-concurrency token against the event store.
    /// </summary>
    long Version { get; }

    /// <summary>
    /// Events the aggregate has emitted since load/construction that haven't
    /// been persisted yet. The repository reads this on SaveAsync and appends
    /// to the event store, then calls MarkEventsCommitted.
    /// </summary>
    IReadOnlyList<IDomainEvent> PendingEvents { get; }

    void MarkEventsCommitted();

    /// <summary>
    /// Replays a stream of events to rebuild state. Called once at load.
    /// </summary>
    void LoadFromHistory(IEnumerable<IDomainEvent> history);
}
