using System.Reflection;
using System.Text.Json;
using Games.Meta.Application.Clans;
using Games.Meta.Application.Meta;
using Games.Meta.Application.Quests;
using Games.Meta.Application.Risk;
using Games.Meta.Application.Tournaments;
using Games.Meta.Transport.Grpc.Wire;
using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Games.Meta.Transport.Grpc;

public class MetaGrpcProxy<TContract> : DispatchProxy where TContract : class
{
    private MetaApi.MetaApiClient _client = null!;

    public static TContract Create(MetaApi.MetaApiClient client)
    {
        var proxy = DispatchProxy.Create<TContract, MetaGrpcProxy<TContract>>();
        ((MetaGrpcProxy<TContract>)(object)proxy).SetClient(client);
        return proxy;
    }

    public void SetClient(MetaApi.MetaApiClient client)
    {
        _client = client;
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        ArgumentNullException.ThrowIfNull(targetMethod);
        var returnType = targetMethod.ReturnType;
        var payload = (args ?? []).Where((_, index) =>
            targetMethod.GetParameters()[index].ParameterType != typeof(CancellationToken)).ToArray();
        var ct = (args ?? []).OfType<CancellationToken>().FirstOrDefault();
        var call = new MetaCall
        {
            Service = typeof(TContract).FullName!,
            Method = targetMethod.Name,
            ArgumentsJson = JsonSerializer.Serialize(payload, MetaWire.Options),
        };

        if (returnType == typeof(Task)) return InvokeVoidAsync(call, ct);
        var resultType = returnType.GetGenericArguments().Single();
        return typeof(MetaGrpcProxy<TContract>)
            .GetMethod(nameof(InvokeAsync), BindingFlags.Instance | BindingFlags.NonPublic)!
            .MakeGenericMethod(resultType)
            .Invoke(this, [call, ct]);
    }

    private async Task InvokeVoidAsync(MetaCall call, CancellationToken ct) =>
        _ = await _client.InvokeAsync(call, cancellationToken: ct);

    private async Task<TResult> InvokeAsync<TResult>(MetaCall call, CancellationToken ct)
    {
        var reply = await _client.InvokeAsync(call, cancellationToken: ct);
        return JsonSerializer.Deserialize<TResult>(reply.ResultJson, MetaWire.Options)!;
    }
}
