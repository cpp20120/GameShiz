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

internal static class MetaWire
{
    internal static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}

public sealed class MetaGrpcEndpoint(IServiceProvider services) : MetaApi.MetaApiBase
{
    private static readonly IReadOnlyDictionary<string, Type> Contracts = new[]
    {
        typeof(IMetaService), typeof(IQuestService), typeof(IClanService),
        typeof(ITournamentService), typeof(IRiskService),
    }.ToDictionary(type => type.FullName!, StringComparer.Ordinal);

    public override async Task<MetaReply> Invoke(MetaCall request, ServerCallContext context)
    {
        if (!Contracts.TryGetValue(request.Service, out var contract))
            throw new RpcException(new Status(StatusCode.Unimplemented, "Unknown Meta contract."));

        var arguments = JsonSerializer.Deserialize<JsonElement[]>(request.ArgumentsJson, MetaWire.Options) ?? [];
        var method = contract.GetMethods().SingleOrDefault(candidate =>
            candidate.Name == request.Method &&
            candidate.GetParameters().Count(parameter => parameter.ParameterType != typeof(CancellationToken)) == arguments.Length);
        if (method is null)
            throw new RpcException(new Status(StatusCode.Unimplemented, "Unknown Meta operation."));

        var parameters = method.GetParameters();
        var values = new object?[parameters.Length];
        var argumentIndex = 0;
        for (var index = 0; index < parameters.Length; index++)
        {
            values[index] = parameters[index].ParameterType == typeof(CancellationToken)
                ? context.CancellationToken
                : JsonSerializer.Deserialize(arguments[argumentIndex++].GetRawText(), parameters[index].ParameterType, MetaWire.Options);
        }

        var invocation = method.Invoke(services.GetRequiredService(contract), values)
            ?? throw new RpcException(new Status(StatusCode.Internal, "Meta operation returned no task."));
        var task = (Task)invocation;
        await task.ConfigureAwait(false);
        var result = task.GetType().GetProperty("Result")?.GetValue(task);
        return new MetaReply { ResultJson = JsonSerializer.Serialize(result, result?.GetType() ?? typeof(object), MetaWire.Options) };
    }
}

public class MetaGrpcProxy<TContract> : DispatchProxy where TContract : class
{
    private MetaApi.MetaApiClient _client = null!;

    public static TContract Create(MetaApi.MetaApiClient client)
    {
        var proxy = Create<TContract, MetaGrpcProxy<TContract>>();
        ((MetaGrpcProxy<TContract>)(object)proxy)._client = client;
        return proxy;
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
