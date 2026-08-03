using BotFramework.Contracts.Messaging;
using DotNetCore.CAP;

namespace BotFramework.Host.Messaging;

public sealed class CapIntegrationCommandConsumer(IntegrationCommandDispatcher dispatcher) : ICapSubscribe
{
    [CapSubscribe(IntegrationMessagingTopics.Commands)]
    public Task HandleAsync(IntegrationCommandEnvelope envelope, CancellationToken ct) =>
        dispatcher.DispatchAsync(envelope, ct);
}
