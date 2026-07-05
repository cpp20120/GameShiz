using Games.Poker.Application.Services;
using Games.Poker.Transport.Grpc.Wire;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Games.Poker.Transport.Grpc;

public static class PokerGrpcExtensions
{
    public static IServiceCollection AddPokerGrpcClient(this IServiceCollection services, Uri address)
    {
        services.AddSingleton(_ => GrpcChannel.ForAddress(address));
        services.AddSingleton(sp => new PokerApi.PokerApiClient(sp.GetRequiredService<GrpcChannel>()));
        services.AddScoped<IPokerService, GrpcPokerService>();
        return services;
    }

    public static IEndpointRouteBuilder MapPokerGrpcTransport(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGrpcService<PokerGrpcEndpoint>();
        return endpoints;
    }
}
