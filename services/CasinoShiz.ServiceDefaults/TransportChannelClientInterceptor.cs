using BotFramework.Contracts.Messaging;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.Extensions.Configuration;

namespace CasinoShiz.ServiceDefaults;

/// <summary>Adds the logical bot channel to every outbound gRPC request.</summary>
public sealed class TransportChannelClientInterceptor(IConfiguration? configuration = null) : Interceptor
{
    private const string HeaderName = "x-casino-channel";
    private const string ApiKeyHeaderName = "x-casino-service-key";
    private readonly string _channel = configuration?["Transport:Channel"] ?? "telegram";
    private readonly string? _apiKey = configuration?["Backend:ApiKey"]
        ?? configuration?["Services:Backend:ApiKey"];

    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
    {
        var headers = new Metadata();
        if (context.Options.Headers is not null)
        {
            foreach (var entry in context.Options.Headers)
                headers.Add(entry);
        }

        if (!headers.Any(entry => string.Equals(entry.Key, HeaderName, StringComparison.OrdinalIgnoreCase)))
            headers.Add(HeaderName, _channel);

        var requestMetadata = RequestMetadataContext.TryGetCurrent();
        if (requestMetadata is not null)
        {
            AddContextHeader(headers, "tenant_id", requestMetadata.Tenant?.ToString());
            AddContextHeader(headers, "scope_id", requestMetadata.TypedScope?.ToString() ?? requestMetadata.ScopeId);
            AddContextHeader(headers, "player_id", requestMetadata.Player?.ToString() ?? requestMetadata.UserId);
            AddContextHeader(headers, "request_id", requestMetadata.RequestId);
            AddContextHeader(headers, "correlation_id", requestMetadata.CorrelationId);
            AddContextHeader(headers, "channel", requestMetadata.Channel.ToString().ToLowerInvariant());
        }

        if (!string.IsNullOrWhiteSpace(_apiKey)
            && !headers.Any(entry => string.Equals(entry.Key, ApiKeyHeaderName, StringComparison.OrdinalIgnoreCase)))
            headers.Add(ApiKeyHeaderName, _apiKey);

        var options = context.Options.WithHeaders(headers);
        return continuation(request, new ClientInterceptorContext<TRequest, TResponse>(
            context.Method, context.Host, options));
    }

    private static void AddContextHeader(Metadata headers, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value)
            && !headers.Any(entry => string.Equals(entry.Key, name, StringComparison.OrdinalIgnoreCase)))
            headers.Add(name, value);
    }
}
