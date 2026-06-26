
namespace CasinoShiz.Host.Debug;

public sealed class DebugEsSmokeAggregate(string id) : IEventSourcedAggregate
{
    private readonly List<IDomainEvent> _pending = [];

    public DebugEsSmokeAggregate() : this(string.Empty)
    {
    }

    public string Id { get; private set; } = id;
    public long Version { get; private set; }
    public int Count { get; private set; }
    public IReadOnlyList<IDomainEvent> PendingEvents => _pending;

    public void Increment(long userId, long chatId)
    {
        Apply(new DebugEsSmokeIncremented(
            Id,
            Count + 1,
            userId,
            chatId,
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()),
            isNew: true);
    }

    public void MarkEventsCommitted() => _pending.Clear();

    public void LoadFromHistory(IEnumerable<IDomainEvent> history)
    {
        foreach (var ev in history)
            Apply(ev, isNew: false);
    }

    private void Apply(IDomainEvent ev, bool isNew)
    {
        switch (ev)
        {
            case DebugEsSmokeIncremented incremented:
                Id = incremented.StreamId;
                Count = incremented.Count;
                Version++;
                break;
            default:
                throw new InvalidOperationException($"Unsupported debug ES event '{ev.EventType}'.");
        }

        if (isNew)
            _pending.Add(ev);
    }
}
