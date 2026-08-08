using BotFramework.Host.Contracts.Economics;
using BotFramework.Host.Contracts.ResponsibleGaming;
using CasinoShiz.ServiceDefaults;
using CasinoShiz.Wallet.Transport.Grpc.Wire;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace CasinoShiz.Wallet.Transport.Grpc;

public static class WalletGrpcExtensions
{
    public static IServiceCollection AddWalletGrpcClients(this IServiceCollection services, Uri address)
    {
        services.AddResilientGrpcClient<WalletApi.WalletApiClient>(address);
        services.AddSingleton(provider => WalletGrpcProxyFactory.Create<IEconomicsService>(provider.GetRequiredService<WalletApi.WalletApiClient>()));
        services.AddSingleton(provider => WalletGrpcProxyFactory.Create<IWalletAtomicExecutionService>(provider.GetRequiredService<WalletApi.WalletApiClient>()));
        services.AddSingleton(provider => WalletGrpcProxyFactory.Create<IWalletSnapshotService>(provider.GetRequiredService<WalletApi.WalletApiClient>()));
        services.AddSingleton(provider => WalletGrpcProxyFactory.Create<IDailyBonusService>(provider.GetRequiredService<WalletApi.WalletApiClient>()));
        services.AddSingleton(provider => WalletGrpcProxyFactory.Create<IWalletReadService>(provider.GetRequiredService<WalletApi.WalletApiClient>()));
        services.AddSingleton(provider => WalletGrpcProxyFactory.Create<IWalletAnalyticsService>(provider.GetRequiredService<WalletApi.WalletApiClient>()));
        services.AddSingleton(provider => WalletGrpcProxyFactory.Create<IPlayerProtectionService>(provider.GetRequiredService<WalletApi.WalletApiClient>()));
        return services;
    }

    public static IEndpointRouteBuilder MapWalletGrpcTransport(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGrpcService<WalletGrpcEndpoint>();
        return endpoints;
    }
}
