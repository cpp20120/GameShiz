using BotFramework.Contracts.Messaging;
using DotNetCore.CAP;

namespace BotFramework.Host.Messaging;

public sealed class CapIntegrationEventConsumer(IntegrationEventDispatcher dispatcher) : ICapSubscribe
{
    [CapSubscribe(IntegrationMessagingTopics.Events)]
    public Task HandleAsync(IntegrationEventEnvelope envelope, CancellationToken ct) =>
        dispatcher.DispatchAsync(envelope, ct);
}
