using BotFramework.Sdk;

namespace BotFramework.Host.Events.Stores;

internal sealed class EventLogSubscriber(IEventLog log) : IDomainEventSubscriber
{
    public Task HandleAsync(IDomainEvent ev, CancellationToken ct) => log.AppendAsync(ev, ct);
}
