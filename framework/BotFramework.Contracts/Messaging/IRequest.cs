namespace BotFramework.Contracts.Messaging;

/// <summary>
/// A transport-neutral request with exactly one logical handler.
/// Transports may carry it over gRPC in-process or across a network boundary.
/// </summary>
public interface IRequest<out TResponse>
{
    string MessageType { get; }
    Type ResponseType => typeof(TResponse);
}
