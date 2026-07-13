using CasinoShiz.ServiceDefaults;
using Games.PixelBattle.Contracts;
using Games.PixelBattle.Transport.Grpc.Wire;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Games.PixelBattle.Transport.Grpc;

public static class PixelBattleGrpcExtensions
{
    public static IServiceCollection AddPixelBattleGrpcClient(this IServiceCollection services, Uri address)
    {
        services.AddResilientGrpcClient<PixelBattleApi.PixelBattleApiClient>(address);
        services.AddScoped<IPixelBattleService, GrpcPixelBattleClient>();
        return services;
    }

    public static IEndpointRouteBuilder MapPixelBattleGrpcTransport(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGrpcService<PixelBattleGrpcEndpoint>();
        return endpoints;
    }
}
