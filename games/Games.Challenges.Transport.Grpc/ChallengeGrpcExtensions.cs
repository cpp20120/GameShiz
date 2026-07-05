using Games.Challenges.Application.Services;
using Games.Challenges.Transport.Grpc.Wire;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Games.Challenges.Transport.Grpc;

public static class ChallengeGrpcExtensions
{
    public static IServiceCollection AddChallengeGrpcClient(this IServiceCollection services, Uri address)
    {
        services.AddSingleton(_ => GrpcChannel.ForAddress(address));
        services.AddSingleton(serviceProvider =>
            new ChallengeApi.ChallengeApiClient(serviceProvider.GetRequiredService<GrpcChannel>()));
        services.AddScoped<IChallengeService, GrpcChallengeService>();
        return services;
    }

    public static IEndpointRouteBuilder MapChallengeGrpcTransport(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGrpcService<ChallengeGrpcEndpoint>();
        return endpoints;
    }
}
