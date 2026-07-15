using BotFramework.Sdk.Domain;
using BotFramework.Sdk.Events.Contracts;

namespace SampleGame.Infrastructure;

/// <summary>Minimal event-sourced aggregate hook for the generated module.</summary>
public sealed class SampleGameAggregate(string id) : IEventSourcedAggregate
{
    private readonly List<IDomainEvent> pending = [];

    public string Id { get; } = string.IsNullOrWhiteSpace(id)
        ? throw new ArgumentException("Aggregate id is required.", nameof(id))
        : id;

    public long Version { get; private set; }

    public IReadOnlyList<IDomainEvent> PendingEvents => pending;

    public void Play() => Apply(new SampleGamePlayed(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));

    public void LoadFromHistory(IEnumerable<IDomainEvent> history)
    {
        ArgumentNullException.ThrowIfNull(history);
        foreach (var domainEvent in history)
            Apply(domainEvent, addToPending: false);
    }

    public void MarkEventsCommitted() => pending.Clear();

    private void Apply(IDomainEvent domainEvent, bool addToPending = true)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        Version++;
        if (addToPending)
            pending.Add(domainEvent);
    }
}

public sealed record SampleGamePlayed(long OccurredAt) : IDomainEvent
{
    public string EventType => "sample-game.played";
}
