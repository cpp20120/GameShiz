namespace BotFramework.Contracts.Messaging;

public interface IIntegrationMessageRouter
{
    IntegrationMessageRoute Route(
        IntegrationMessageKind kind,
        string messageType,
        object message,
        string? tenantId,
        string? scopeId,
        string? playerId);
}
