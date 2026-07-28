using System.Reflection;
using CasinoShiz.Wallet.Transport.Grpc.Wire;

namespace CasinoShiz.Wallet.Transport.Grpc;

internal static class WalletGrpcProxyFactory
{
    internal static TContract Create<TContract>(WalletApi.WalletApiClient client) where TContract : class
    {
        var proxy = DispatchProxy.Create<TContract, WalletGrpcProxy<TContract>>();
        ((WalletGrpcProxy<TContract>)(object)proxy).SetClient(client);
        return proxy;
    }
}
