using System.Reflection;
using Games.Admin.Transport.Grpc.Wire;

namespace Games.Admin.Transport.Grpc;

internal static class AdminGrpcProxyFactory
{
    internal static TContract Create<TContract>(AdminApi.AdminApiClient client) where TContract : class
    {
        var proxy = DispatchProxy.Create<TContract, AdminGrpcProxy<TContract>>();
        ((AdminGrpcProxy<TContract>)(object)proxy).SetClient(client);
        return proxy;
    }
}
