using CasinoShiz.ServiceDefaults;
using Games.Pick.Application.Services;
using Games.Pick.Transport.Grpc.Wire;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
namespace Games.Pick.Transport.Grpc;
public static class PickGrpcExtensions
{
    public static IServiceCollection AddPickGrpcClient(this IServiceCollection s,Uri a){s.AddResilientGrpcClient<PickApi.PickApiClient>(a);s.AddScoped<IPickClient,GrpcPickClient>();return s;}
    public static IEndpointRouteBuilder MapPickGrpcTransport(this IEndpointRouteBuilder e){e.MapGrpcService<PickGrpcEndpoint>();return e;}
}
