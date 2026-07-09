using CasinoShiz.ServiceDefaults;
using BotFramework.Host.Analytics.Reports;
using Games.Admin.Application.Services;
using Games.Admin.Infrastructure.Persistence;
using Games.Admin.Transport.Grpc.Wire;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Games.Admin.Transport.Grpc;

public static class AdminGrpcExtensions
{
    public static IServiceCollection AddAdminGrpcClients(this IServiceCollection services, Uri address)
    {
        services.AddResilientGrpcClient<AdminApi.AdminApiClient>(address);
        services.AddScoped(provider => AdminGrpcProxy<IAdminService>.Create(provider.GetRequiredService<AdminApi.AdminApiClient>()));
        services.AddScoped(provider => AdminGrpcProxy<IChatsStore>.Create(provider.GetRequiredService<AdminApi.AdminApiClient>()));
        services.AddScoped(provider => AdminGrpcProxy<IAnalyticsQueryService>.Create(provider.GetRequiredService<AdminApi.AdminApiClient>()));
        return services;
    }

    public static IEndpointRouteBuilder MapAdminGrpcTransport(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGrpcService<AdminGrpcEndpoint>();
        return endpoints;
    }
}
