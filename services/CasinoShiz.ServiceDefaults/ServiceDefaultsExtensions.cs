using CasinoShiz.ServiceDefaults;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace CasinoShiz.ServiceDefaults;

public static class ServiceDefaultsExtensions
{
    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddHealthChecks();
        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics => metrics
                .AddMeter("CasinoShiz.GameExecution")
                .AddMeter("CasinoShiz.Rendering")
                .AddRuntimeInstrumentation()
                .AddHttpClientInstrumentation()
                .AddAspNetCoreInstrumentation()
                .AddOtlpExporter())
            .WithTracing(tracing => tracing
                .AddSource("CasinoShiz.GameExecution")
                .AddHttpClientInstrumentation()
                .AddAspNetCoreInstrumentation()
                .AddOtlpExporter());
        return builder;
    }

    public static IHttpClientBuilder AddResilientGrpcClient<TClient>(
        this IServiceCollection services,
        Uri address)
        where TClient : class
    {
        services.TryAddSingleton<TransportChannelClientInterceptor>();
        var client = services.AddGrpcClient<TClient>(options => options.Address = address);
        client.AddInterceptor<TransportChannelClientInterceptor>();
        client.AddStandardResilienceHandler(options =>
        {
            options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(10);
            options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(30);
            options.Retry.MaxRetryAttempts = 3;
        });
        return client;
    }

    /// <summary>
    /// Registers a named resilient gRPC client. Named clients are used by the
    /// channel BFFs when a game is deployed as its own backend service. The
    /// default overload above remains the monolith/single-backend path.
    /// </summary>
    public static IHttpClientBuilder AddResilientGrpcClient<TClient>(
        this IServiceCollection services,
        string name,
        Uri address)
        where TClient : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(address);

        services.TryAddSingleton<TransportChannelClientInterceptor>();
        var client = services.AddGrpcClient<TClient>(name, options => options.Address = address);
        client.AddInterceptor<TransportChannelClientInterceptor>();
        client.AddStandardResilienceHandler(options =>
        {
            options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(10);
            options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(30);
            options.Retry.MaxRetryAttempts = 3;
        });
        return client;
    }

    public static IEndpointRouteBuilder MapServiceDefaults(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthChecks("/health/live", new() { Predicate = _ => false });
        endpoints.MapHealthChecks("/health/ready");
        return endpoints;
    }
}
