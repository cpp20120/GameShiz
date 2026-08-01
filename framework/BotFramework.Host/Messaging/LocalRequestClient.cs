using BotFramework.Contracts.Messaging;
using BotFramework.Contracts.Tenancy;
using MediatR;

namespace BotFramework.Host.Messaging;

/// <summary>
/// In-process request transport backed by MediatR. A gRPC client implements the
/// same IRequestClient port when a bounded context moves out of process.
/// </summary>
public sealed class LocalRequestClient(ISender sender, ITenantContextAccessor? tenantContext = null) : IRequestClient
{
    public async Task<TResponse> SendAsync<TRequest, TResponse>(
        TRequest request,
        RequestMetadata metadata,
        CancellationToken ct)
        where TRequest : BotFramework.Contracts.Messaging.IRequest<TResponse>
    {
        using var metadataScope = RequestMetadataContext.Push(metadata);
        using var tenantScope = metadata.TenantContext is { } tenant && tenantContext is not null
            ? tenantContext.Push(tenant)
            : null;
        return await sender.Send(request, ct);
    }
}
