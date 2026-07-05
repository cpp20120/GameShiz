using BotFramework.Contracts.Messaging;
using Microsoft.Extensions.DependencyInjection;

namespace BotFramework.Host.Messaging;

/// <summary>
/// In-process request transport used while bounded contexts still share a process.
/// A gRPC client implements the same port when a context moves out of process.
/// </summary>
public sealed class LocalRequestClient(IServiceProvider services) : IRequestClient
{
    public Task<TResponse> SendAsync<TRequest, TResponse>(
        TRequest request,
        RequestMetadata metadata,
        CancellationToken ct)
        where TRequest : IRequest<TResponse> =>
        services.GetRequiredService<IRequestHandler<TRequest, TResponse>>()
            .HandleAsync(request, metadata, ct);
}
