using CasinoShiz.ServiceDefaults;
using Games.Basketball.Application.Services;
using Games.Bowling.Application.Services;
using Games.Darts.Application.Services;
using Games.DiceCube.Application.Services;
using Games.Football.Application.Services;
using Games.NativeDice.Transport.Grpc.Wire;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Games.NativeDice.Transport.Grpc;

public static class NativeDiceGrpcExtensions
{
    public static IServiceCollection AddNativeDiceGrpcClients(
        this IServiceCollection services,
        Uri backendAddress)
    {
        services.AddResilientGrpcClient<NativeDiceApi.NativeDiceApiClient>(backendAddress);
        services.AddScoped<IDiceCubeService, GrpcDiceCubeService>();
        services.AddScoped<IDartsService, GrpcDartsService>();
        services.AddScoped<IFootballService, GrpcFootballService>();
        services.AddScoped<IBasketballService, GrpcBasketballService>();
        services.AddScoped<IBowlingService, GrpcBowlingService>();
        return services;
    }

    public static IEndpointRouteBuilder MapNativeDiceGrpcTransport(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGrpcService<NativeDiceGrpcEndpoint>();
        return endpoints;
    }
}
