using BotFramework.Contracts.Operations;
using CasinoShiz.Operations.Transport.Grpc.Wire;
using CasinoShiz.ServiceDefaults;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace CasinoShiz.Operations.Transport.Grpc;

public static class OperationsGrpcExtensions
{
    public static IServiceCollection AddOperationsGrpcClient(this IServiceCollection services, Uri address, string apiKey)
    {
        services.AddResilientGrpcClient<OperationsApi.OperationsApiClient>(address);
        services.AddScoped<IOperationsAdminService>(provider =>
            OperationsGrpcProxy.Create(provider.GetRequiredService<OperationsApi.OperationsApiClient>(), apiKey));
        return services;
    }

    public static IEndpointRouteBuilder MapOperationsGrpcTransport(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGrpcService<OperationsGrpcEndpoint>();
        return endpoints;
    }
}
