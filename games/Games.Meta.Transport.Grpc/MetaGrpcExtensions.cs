using Games.Meta.Application.Clans;
using Games.Meta.Application.Meta;
using Games.Meta.Application.Quests;
using Games.Meta.Application.Risk;
using Games.Meta.Application.Tournaments;
using Games.Meta.Transport.Grpc.Wire;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Games.Meta.Transport.Grpc;

public static class MetaGrpcExtensions
{
    public static IServiceCollection AddMetaGrpcClients(this IServiceCollection services, Uri address)
    {
        services.AddSingleton(_ => GrpcChannel.ForAddress(address));
        services.AddSingleton(provider => new MetaApi.MetaApiClient(provider.GetRequiredService<GrpcChannel>()));
        services.AddScoped(provider => MetaGrpcProxy<IMetaService>.Create(provider.GetRequiredService<MetaApi.MetaApiClient>()));
        services.AddScoped(provider => MetaGrpcProxy<IQuestService>.Create(provider.GetRequiredService<MetaApi.MetaApiClient>()));
        services.AddScoped(provider => MetaGrpcProxy<IClanService>.Create(provider.GetRequiredService<MetaApi.MetaApiClient>()));
        services.AddScoped(provider => MetaGrpcProxy<ITournamentService>.Create(provider.GetRequiredService<MetaApi.MetaApiClient>()));
        services.AddScoped(provider => MetaGrpcProxy<IRiskService>.Create(provider.GetRequiredService<MetaApi.MetaApiClient>()));
        return services;
    }

    public static IEndpointRouteBuilder MapMetaGrpcTransport(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGrpcService<MetaGrpcEndpoint>();
        return endpoints;
    }
}
