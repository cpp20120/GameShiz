using Games.Blackjack.Contracts;using Games.Blackjack.Transport.Grpc.Wire;using Grpc.Net.Client;using Microsoft.AspNetCore.Builder;using Microsoft.AspNetCore.Routing;using Microsoft.Extensions.DependencyInjection;
namespace Games.Blackjack.Transport.Grpc;
public static class BlackjackGrpcExtensions
{public static IServiceCollection AddBlackjackGrpcClient(this IServiceCollection s,Uri a){s.AddSingleton(_=>GrpcChannel.ForAddress(a));s.AddSingleton(sp=>new BlackjackApi.BlackjackApiClient(sp.GetRequiredService<GrpcChannel>()));s.AddScoped<IBlackjackClient,GrpcBlackjackClient>();return s;}public static IEndpointRouteBuilder MapBlackjackGrpcTransport(this IEndpointRouteBuilder e){e.MapGrpcService<BlackjackGrpcEndpoint>();return e;}}
