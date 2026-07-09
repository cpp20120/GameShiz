using CasinoShiz.ServiceDefaults;
using Games.Transfer.Application.Services;
using Games.Transfer.Transport.Grpc.Wire;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Games.Transfer.Transport.Grpc;

public static class TransferGrpcExtensions
{
    public static IServiceCollection AddTransferGrpcClient(this IServiceCollection services, Uri address)
    {
        services.AddResilientGrpcClient<TransferApi.TransferApiClient>(address);
        services.AddScoped<ITransferService, GrpcTransferService>();
        return services;
    }

    public static IEndpointRouteBuilder MapTransferGrpcTransport(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGrpcService<TransferGrpcEndpoint>();
        return endpoints;
    }
}
