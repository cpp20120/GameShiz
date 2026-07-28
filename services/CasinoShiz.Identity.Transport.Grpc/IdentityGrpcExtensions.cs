using BotFramework.Contracts.Identity;
using CasinoShiz.Identity.Transport.Grpc.Wire;
using CasinoShiz.ServiceDefaults;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace CasinoShiz.Identity.Transport.Grpc;

public static class IdentityGrpcExtensions
{
    public static IServiceCollection AddIdentityGrpcClient(this IServiceCollection services, Uri address)
    {
        services.AddResilientGrpcClient<IdentityApi.IdentityApiClient>(address);
        services.AddSingleton<IPlayerDirectory, GrpcPlayerDirectory>();
        return services;
    }

    public static IEndpointRouteBuilder MapIdentityGrpcTransport(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGrpcService<IdentityGrpcEndpoint>();
        return endpoints;
    }
}