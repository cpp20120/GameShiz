namespace BotFramework.Contracts.Messaging;

public interface IIntegrationCommandPublisher
{
    Task SendAsync<TCommand>(TCommand command, CancellationToken ct)
        where TCommand : IIntegrationCommand;
}
