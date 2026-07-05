namespace BotFramework.Contracts.Messaging;

public interface IRequestHandler<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    Task<TResponse> HandleAsync(TRequest request, RequestMetadata metadata, CancellationToken ct);
}
