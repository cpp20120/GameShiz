using Games.Leaderboard.Contracts;
using Games.Leaderboard.Transport.Grpc.Wire;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
namespace Games.Leaderboard.Transport.Grpc;
public static class LeaderboardGrpcExtensions
{
    public static IServiceCollection AddLeaderboardGrpcClient(this IServiceCollection services, Uri address) { services.AddSingleton(_ => GrpcChannel.ForAddress(address)); services.AddSingleton(sp => new LeaderboardApi.LeaderboardApiClient(sp.GetRequiredService<GrpcChannel>())); services.AddScoped<ILeaderboardClient, GrpcLeaderboardClient>(); return services; }
    public static IEndpointRouteBuilder MapLeaderboardGrpcTransport(this IEndpointRouteBuilder endpoints) { endpoints.MapGrpcService<LeaderboardGrpcEndpoint>(); return endpoints; }
}
