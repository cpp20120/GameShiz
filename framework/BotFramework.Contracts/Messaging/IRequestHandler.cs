namespace BotFramework.Contracts.Messaging;

/// <summary>
/// Compatibility-facing handler contract. MediatR invokes the default bridge,
/// while existing handlers keep receiving transport metadata explicitly.
/// </summary>
public interface IRequestHandler<in TRequest, TResponse> : MediatR.IRequestHandler<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    Task<TResponse> HandleAsync(TRequest request, RequestMetadata metadata, CancellationToken ct);

    Task<TResponse> MediatR.IRequestHandler<TRequest, TResponse>.Handle(
        TRequest request,
        CancellationToken cancellationToken) =>
        HandleAsync(request, RequestMetadataContext.Current, cancellationToken);
}
