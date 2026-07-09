using CasinoShiz.ServiceDefaults;
using Games.Redeem.Contracts;
using Games.Redeem.Transport.Grpc.Wire;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Games.Redeem.Transport.Grpc;
public static class RedeemGrpcExtensions
{
    public static IServiceCollection AddRedeemGrpcClient(this IServiceCollection services, Uri address)
    {
        services.AddResilientGrpcClient<RedeemApi.RedeemApiClient>(address);
        services.AddScoped<IRedeemClient, GrpcRedeemClient>();
        return services;
    }
    public static IEndpointRouteBuilder MapRedeemGrpcTransport(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGrpcService<RedeemGrpcEndpoint>();
        return endpoints;
    }
}
