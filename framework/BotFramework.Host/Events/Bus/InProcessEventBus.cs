using Microsoft.Extensions.DependencyInjection;

namespace BotFramework.Host.Events.Bus;

public sealed class InProcessEventBus : IDomainEventBus
{
    private readonly DomainEventSubscriptionDispatcher _dispatcher;

    [ActivatorUtilitiesConstructor]
    public InProcessEventBus(DomainEventSubscriptionDispatcher dispatcher) => _dispatcher = dispatcher;

    public InProcessEventBus(ILogger<InProcessEventBus> logger) =>
        _dispatcher = new DomainEventSubscriptionDispatcher(logger);

    public void Subscribe(string eventTypePattern, IDomainEventSubscriber subscriber) =>
        _dispatcher.Subscribe(eventTypePattern, subscriber);

    public Task PublishAsync(IDomainEvent ev, CancellationToken ct) =>
        _dispatcher.DispatchAsync(ev, ct);
}
