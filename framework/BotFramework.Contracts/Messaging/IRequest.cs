using MediatR;

namespace BotFramework.Contracts.Messaging;

/// <summary>
/// A transport-neutral request with exactly one logical handler.
/// Transports may carry it over gRPC, in-process, or across a network boundary.
/// MediatR is the in-process implementation detail; callers depend on IRequestClient.
/// </summary>
public interface IRequest<out TResponse> : MediatR.IRequest<TResponse>
{
    string MessageType { get; }
    Type ResponseType => typeof(TResponse);
}
