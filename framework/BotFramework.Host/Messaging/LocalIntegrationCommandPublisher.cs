using BotFramework.Contracts.Messaging;

namespace BotFramework.Host.Messaging;

public sealed class LocalIntegrationCommandPublisher(IServiceProvider services)
    : IIntegrationCommandPublisher
{
    public async Task SendAsync<TCommand>(TCommand command, CancellationToken ct)
        where TCommand : IIntegrationCommand
    {
        var handlers = services.GetServices<IIntegrationCommandHandler<TCommand>>();
        foreach (var handler in handlers)
            await handler.HandleAsync(command, ct);
    }
}
