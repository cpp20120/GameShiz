using DotNetCore.CAP;

namespace BotFramework.Host.Events.Bus;

public sealed class CapEventConsumer(CapEventBus bus) : ICapSubscribe
{
    [CapSubscribe(CapEventBus.Topic)]
    public Task HandleAsync(DomainEventEnvelope envelope, CancellationToken ct) =>
        bus.DispatchAsync(envelope, ct);
}
