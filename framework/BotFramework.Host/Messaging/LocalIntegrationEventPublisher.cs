using BotFramework.Contracts.Messaging;
using Microsoft.Extensions.DependencyInjection;

namespace BotFramework.Host.Messaging;

/// <summary>
/// In-process adapter for the integration-event contract. A broker adapter can
/// replace it at the composition root without changing publishers or handlers.
/// </summary>
public sealed class LocalIntegrationEventPublisher(IServiceProvider services)
    : IIntegrationEventPublisher
{
    public async Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken ct)
        where TEvent : IIntegrationEvent
    {
        var handlers = services.GetServices<IIntegrationEventHandler<TEvent>>();
        foreach (var handler in handlers)
            await handler.HandleAsync(integrationEvent, ct);
    }
}
