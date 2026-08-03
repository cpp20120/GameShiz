namespace BotFramework.Contracts.Messaging;

public interface IIntegrationCommandHandler<in TCommand>
    where TCommand : IIntegrationCommand
{
    Task HandleAsync(TCommand command, CancellationToken ct);
}
