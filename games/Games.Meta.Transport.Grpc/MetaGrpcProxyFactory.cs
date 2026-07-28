using System.Reflection;
using Games.Meta.Transport.Grpc.Wire;

namespace Games.Meta.Transport.Grpc;

internal static class MetaGrpcProxyFactory
{
    public static TContract Create<TContract>(MetaApi.MetaApiClient client) where TContract : class
    {
        var proxy = DispatchProxy.Create<TContract, MetaGrpcProxy<TContract>>();
        ((MetaGrpcProxy<TContract>)(object)proxy).SetClient(client);
        return proxy;
    }
}
