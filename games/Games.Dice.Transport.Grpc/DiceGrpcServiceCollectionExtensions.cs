using Games.Dice.Contracts.Play;
using Games.Dice.Transport.Grpc.Wire;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Games.Dice.Transport.Grpc;

/// <summary>
/// gRPC composition adapter. Consumers continue to resolve only IDiceClient.
/// </summary>
public static class DiceGrpcServiceCollectionExtensions
{
    public static IServiceCollection AddDiceGrpcClient(
        this IServiceCollection services,
        Uri backendAddress)
    {
        services.AddSingleton(_ => GrpcChannel.ForAddress(backendAddress));
        services.AddSingleton(sp =>
            new DiceApi.DiceApiClient(sp.GetRequiredService<GrpcChannel>()));
        services.AddScoped<IDiceClient, GrpcDiceClient>();
        return services;
    }

    public static IEndpointRouteBuilder MapDiceGrpcTransport(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGrpcService<DiceGrpcEndpoint>();
        return endpoints;
    }
}
