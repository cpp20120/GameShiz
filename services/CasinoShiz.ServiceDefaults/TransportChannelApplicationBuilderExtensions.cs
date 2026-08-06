using BotFramework.Contracts.Messaging;
using BotFramework.Contracts.Tenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace CasinoShiz.ServiceDefaults;

public static class TransportChannelApplicationBuilderExtensions
{
    public static IApplicationBuilder UseTransportChannelContext(this IApplicationBuilder app) =>
        app.Use(async (httpContext, next) =>
        {
            var value = httpContext.Request.Headers["x-casino-channel"].FirstOrDefault();
            var channel = Enum.TryParse<BotChannel>(value, ignoreCase: true, out var parsed)
                && Enum.IsDefined(parsed)
                ? parsed
                : BotChannel.Telegram;
            using var metadataScope = TryPushRequestMetadata(httpContext, channel);
            await next();
        });

    private static TransportContextScope? TryPushRequestMetadata(HttpContext context, BotChannel channel)
    {
        var tenantValue = context.Request.Headers["tenant_id"].FirstOrDefault();
        var scopeValue = context.Request.Headers["scope_id"].FirstOrDefault();
        if (!TenantId.TryParse(tenantValue, null, out var tenant)
            || !ScopeId.TryParse(scopeValue, null, out var scope))
            return null;

        PlayerId? player = null;
        var playerValue = context.Request.Headers["player_id"].FirstOrDefault();
        if (PlayerId.TryParse(playerValue, null, out var parsedPlayer))
            player = parsedPlayer;

        var requestId = HeaderOrFallback(context, "request_id");
        var correlationId = HeaderOrFallback(context, "correlation_id", requestId);
        var tenantContext = TenantContext.Create(
            tenant,
            scope,
            player,
            channel,
            RequestId.Create(requestId),
            RequestId.Create(correlationId));
        var metadata = new RequestMetadata(
            requestId,
            correlationId,
            "grpc",
            player?.Value,
            scope.Value,
            "en",
            new Dictionary<string, string>(StringComparer.Ordinal))
        {
            Tenant = tenant,
            TypedScope = scope,
            Player = player,
            Channel = channel,
            TenantContext = tenantContext,
        };
        var metadataScope = RequestMetadataContext.Push(metadata);
        var tenantScope = context.RequestServices
            .GetService<ITenantContextAccessor>()?
            .Push(tenantContext);
        return new TransportContextScope(metadataScope, tenantScope);
    }

    private sealed class TransportContextScope(
        IDisposable metadataScope,
        IDisposable? tenantScope) : IDisposable
    {
        private bool disposed;

        public void Dispose()
        {
            if (disposed)
                return;

            tenantScope?.Dispose();
            metadataScope.Dispose();
            disposed = true;
        }
    }

    private static string HeaderOrFallback(HttpContext context, string name, string? fallback = null)
    {
        var value = context.Request.Headers[name].FirstOrDefault();
        return !string.IsNullOrWhiteSpace(value) && value.Length <= 128 && !value.Any(char.IsControl)
            ? value
            : fallback ?? context.TraceIdentifier;
    }
}
