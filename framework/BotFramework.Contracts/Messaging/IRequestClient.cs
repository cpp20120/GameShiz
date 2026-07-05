namespace BotFramework.Contracts.Messaging;

/// <summary>
/// Logical command/query port. A local dispatcher and a gRPC client are
/// interchangeable implementations from a bounded context's perspective.
/// </summary>
public interface IRequestClient
{
    Task<TResponse> SendAsync<TRequest, TResponse>(
        TRequest request,
        RequestMetadata metadata,
        CancellationToken ct)
        where TRequest : IRequest<TResponse>;
}
