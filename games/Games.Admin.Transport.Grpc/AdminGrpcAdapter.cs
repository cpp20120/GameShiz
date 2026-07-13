using System.Reflection;
using System.Text.Json;
using BotFramework.Host.Analytics.Reports;
using Games.Admin.Application.Services;
using Games.Admin.Infrastructure.Persistence;
using Games.Admin.Transport.Grpc.Wire;
using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Games.Admin.Transport.Grpc;

internal static class AdminWire
{
    internal static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}

public sealed class AdminGrpcEndpoint(IServiceProvider services) : AdminApi.AdminApiBase
{
    private static readonly Dictionary<string, Type> Contracts = new[]
    {
        typeof(IAdminService), typeof(IChatsStore), typeof(IAnalyticsQueryService),
    }.ToDictionary(type => type.FullName!, StringComparer.Ordinal);

    public override async Task<AdminReply> Invoke(AdminCall request, ServerCallContext context)
    {
        if (!Contracts.TryGetValue(request.Service, out var contract))
            throw new RpcException(new Status(StatusCode.Unimplemented, "Unknown Admin contract."));

        var arguments = JsonSerializer.Deserialize<JsonElement[]>(request.ArgumentsJson, AdminWire.Options) ?? [];
        var method = contract.GetMethods().SingleOrDefault(candidate =>
            string.Equals(candidate.Name, request.Method, StringComparison.Ordinal) &&
            candidate.GetParameters().Count(parameter => parameter.ParameterType != typeof(CancellationToken)) == arguments.Length);
        if (method is null)
            throw new RpcException(new Status(StatusCode.Unimplemented, "Unknown Admin operation."));

        var parameters = method.GetParameters();
        var values = new object?[parameters.Length];
        var argumentIndex = 0;
        for (var index = 0; index < parameters.Length; index++)
            values[index] = parameters[index].ParameterType == typeof(CancellationToken)
                ? context.CancellationToken
                : JsonSerializer.Deserialize(arguments[argumentIndex++].GetRawText(), parameters[index].ParameterType, AdminWire.Options);

        var invocation = method.Invoke(services.GetRequiredService(contract), values);
        if (invocation is not Task task)
            return new AdminReply { ResultJson = "null" };

        await task.ConfigureAwait(false);
        var result = task.GetType().GetProperty("Result")?.GetValue(task);
        return new AdminReply { ResultJson = JsonSerializer.Serialize(result, result?.GetType() ?? typeof(object), AdminWire.Options) };
    }
}

public class AdminGrpcProxy<TContract> : DispatchProxy where TContract : class
{
    private AdminApi.AdminApiClient _client = null!;

    public static TContract Create(AdminApi.AdminApiClient client)
    {
        var proxy = Create<TContract, AdminGrpcProxy<TContract>>();
        ((AdminGrpcProxy<TContract>)(object)proxy)._client = client;
        return proxy;
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        ArgumentNullException.ThrowIfNull(targetMethod);
        if (targetMethod.ReturnType == typeof(void)) return null;

        var parameters = targetMethod.GetParameters();
        var payload = (args ?? []).Where((_, index) => parameters[index].ParameterType != typeof(CancellationToken)).ToArray();
        var ct = (args ?? []).OfType<CancellationToken>().FirstOrDefault();
        var call = new AdminCall
        {
            Service = typeof(TContract).FullName!,
            Method = targetMethod.Name,
            ArgumentsJson = JsonSerializer.Serialize(payload, AdminWire.Options),
        };

        if (targetMethod.ReturnType == typeof(Task)) return InvokeVoidAsync(call, ct);
        var resultType = targetMethod.ReturnType.GetGenericArguments().Single();
        return typeof(AdminGrpcProxy<TContract>)
            .GetMethod(nameof(InvokeAsync), BindingFlags.Instance | BindingFlags.NonPublic)!
            .MakeGenericMethod(resultType)
            .Invoke(this, [call, ct]);
    }

    private async Task InvokeVoidAsync(AdminCall call, CancellationToken ct) =>
        _ = await _client.InvokeAsync(call, cancellationToken: ct);

    private async Task<TResult> InvokeAsync<TResult>(AdminCall call, CancellationToken ct)
    {
        var reply = await _client.InvokeAsync(call, cancellationToken: ct);
        return JsonSerializer.Deserialize<TResult>(reply.ResultJson, AdminWire.Options)!;
    }
}
