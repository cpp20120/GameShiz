using BotFramework.Contracts.Messaging;
using MediatR;

namespace BotFramework.Host.Messaging;

/// <summary>
/// In-process request transport backed by MediatR. A gRPC client implements the
/// same IRequestClient port when a bounded context moves out of process.
/// </summary>
public sealed class LocalRequestClient(ISender sender) : IRequestClient
{
    public async Task<TResponse> SendAsync<TRequest, TResponse>(
        TRequest request,
        RequestMetadata metadata,
        CancellationToken ct)
        where TRequest : IRequest<TResponse>
    {
        using var metadataScope = RequestMetadataContext.Push(metadata);
        return await sender.Send(request, ct);
    }
}
