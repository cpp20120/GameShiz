using CasinoShiz.ServiceDefaults;
using Games.Basketball.Application.Services;
using Games.Bowling.Application.Services;
using Games.Darts.Application.Services;
using Games.DiceCube.Application.Services;
using Games.Football.Application.Services;
using Games.NativeDice.Transport.Grpc.Wire;
using Grpc.Net.Client;
using Grpc.Net.ClientFactory;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Games.NativeDice.Transport.Grpc;

public static class NativeDiceGrpcExtensions
{
    public static IServiceCollection AddNativeDiceGrpcClients(
        this IServiceCollection services,
        Uri diceCubeAddress,
        Uri dartsAddress,
        Uri footballAddress,
        Uri basketballAddress,
        Uri bowlingAddress)
    {
        services.AddResilientGrpcClient<NativeDiceApi.NativeDiceApiClient>(
            NativeDiceGrpcClientNames.DiceCube,
            diceCubeAddress);
        services.AddResilientGrpcClient<NativeDiceApi.NativeDiceApiClient>(
            NativeDiceGrpcClientNames.Darts,
            dartsAddress);
        services.AddResilientGrpcClient<NativeDiceApi.NativeDiceApiClient>(
            NativeDiceGrpcClientNames.Football,
            footballAddress);
        services.AddResilientGrpcClient<NativeDiceApi.NativeDiceApiClient>(
            NativeDiceGrpcClientNames.Basketball,
            basketballAddress);
        services.AddResilientGrpcClient<NativeDiceApi.NativeDiceApiClient>(
            NativeDiceGrpcClientNames.Bowling,
            bowlingAddress);

        services.AddScoped<IDiceCubeService, GrpcDiceCubeService>();
        services.AddScoped<IDartsService, GrpcDartsService>();
        services.AddScoped<IFootballService, GrpcFootballService>();
        services.AddScoped<IBasketballService, GrpcBasketballService>();
        services.AddScoped<IBowlingService, GrpcBowlingService>();
        return services;
    }

    public static IServiceCollection AddNativeDiceGrpcClients(
        this IServiceCollection services,
        Uri backendAddress)
        => services.AddNativeDiceGrpcClients(
            backendAddress,
            backendAddress,
            backendAddress,
            backendAddress,
            backendAddress);

    public static IEndpointRouteBuilder MapNativeDiceGrpcTransport(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGrpcService<NativeDiceGrpcEndpoint>();
        return endpoints;
    }
}

public static class NativeDiceGrpcClientNames
{
    public const string DiceCube = "native-dice-dicecube";
    public const string Darts = "native-dice-darts";
    public const string Football = "native-dice-football";
    public const string Basketball = "native-dice-basketball";
    public const string Bowling = "native-dice-bowling";
}
