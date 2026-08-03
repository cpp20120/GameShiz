namespace BotFramework.Contracts.Messaging;

/// <summary>
/// Optional route override for a contract. The framework supplies a stable
/// tenant/scope/type route when a message does not implement this interface.
/// </summary>
public interface IIntegrationMessageRouted
{
    string? Topic { get; }
    string? MessageKey { get; }
}
