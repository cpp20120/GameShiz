using System.Reflection;
using System.Text.Json;
using BotFramework.Contracts.Operations;
using CasinoShiz.Operations.Transport.Grpc.Wire;
using Grpc.Core;

namespace CasinoShiz.Operations.Transport.Grpc;

public class OperationsGrpcProxy : DispatchProxy
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private OperationsApi.OperationsApiClient _client = null!;
    private string _apiKey = null!;

    public static IOperationsAdminService Create(OperationsApi.OperationsApiClient client, string apiKey)
    {
        var proxy = Create<IOperationsAdminService, OperationsGrpcProxy>();
        var instance = (OperationsGrpcProxy)(object)proxy;
        instance._client = client;
        instance._apiKey = apiKey;
        return proxy;
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        ArgumentNullException.ThrowIfNull(targetMethod);
        var parameters = targetMethod.GetParameters();
        var payload = (args ?? [])
            .Where((_, index) => parameters[index].ParameterType != typeof(CancellationToken))
            .ToArray();
        var ct = (args ?? []).OfType<CancellationToken>().FirstOrDefault();
        var call = new OperationsCall
        {
            Method = targetMethod.Name,
            ArgumentsJson = JsonSerializer.Serialize(payload, JsonOptions),
        };
        var resultType = targetMethod.ReturnType.GetGenericArguments().Single();
        return typeof(OperationsGrpcProxy)
            .GetMethod(nameof(Call), BindingFlags.Instance | BindingFlags.NonPublic)!
            .MakeGenericMethod(resultType)
            .Invoke(this, [call, ct]);
    }

    private async Task<T> Call<T>(OperationsCall call, CancellationToken ct)
    {
        var headers = new Metadata { { "x-admin-api-key", _apiKey } };
        var reply = await _client.InvokeAsync(call, headers, cancellationToken: ct);
        return JsonSerializer.Deserialize<T>(reply.ResultJson, JsonOptions)!;
    }
}
