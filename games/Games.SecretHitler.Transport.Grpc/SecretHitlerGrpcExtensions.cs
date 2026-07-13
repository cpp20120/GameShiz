using CasinoShiz.ServiceDefaults;
using Games.SecretHitler.Application.Services;
using Games.SecretHitler.Transport.Grpc.Wire;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Games.SecretHitler.Transport.Grpc;

public static class SecretHitlerGrpcExtensions
{
    public static IServiceCollection AddSecretHitlerGrpcClient(this IServiceCollection services, Uri address)
    {
        services.AddResilientGrpcClient<SecretHitlerApi.SecretHitlerApiClient>(address);
        services.AddScoped<ISecretHitlerService, GrpcSecretHitlerService>();
        return services;
    }

    public static IEndpointRouteBuilder MapSecretHitlerGrpcTransport(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGrpcService<SecretHitlerGrpcEndpoint>();
        return endpoints;
    }
}
