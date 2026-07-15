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
        if (!string.IsNullOrWhiteSpace(_apiKey)
            && !headers.Any(entry => string.Equals(entry.Key, ApiKeyHeaderName, StringComparison.OrdinalIgnoreCase)))
            headers.Add(ApiKeyHeaderName, _apiKey);

        var options = context.Options.WithHeaders(headers);
        return continuation(request, new ClientInterceptorContext<TRequest, TResponse>(
            context.Method, context.Host, options));
    }
}
