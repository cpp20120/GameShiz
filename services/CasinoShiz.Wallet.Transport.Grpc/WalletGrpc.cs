using CasinoShiz.ServiceDefaults;
using System.Reflection;
using System.Text.Json;
using BotFramework.Host.Contracts.Economics;
using BotFramework.Host.Contracts.ResponsibleGaming;
using CasinoShiz.Wallet.Transport.Grpc.Wire;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace CasinoShiz.Wallet.Transport.Grpc;

internal static class WalletWireCodec
{
    internal static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}

public sealed class WalletGrpcEndpoint(IServiceProvider services) : WalletApi.WalletApiBase
{
    private static readonly Dictionary<string, Type> Contracts = new[]
    {
        typeof(IEconomicsService), typeof(IWalletAtomicExecutionService), typeof(IDailyBonusService), typeof(IWalletReadService), typeof(IWalletAnalyticsService), typeof(IPlayerProtectionService),
    }.ToDictionary(type => type.FullName!, StringComparer.Ordinal);

    public override async Task<WalletReply> Invoke(WalletCall request, ServerCallContext context)
    {
        if (!Contracts.TryGetValue(request.Contract, out var contract))
            throw new RpcException(new Status(StatusCode.Unimplemented, "Unknown wallet contract."));
        var arguments = JsonSerializer.Deserialize<JsonElement[]>(request.ArgumentsJson, WalletWireCodec.Options) ?? [];
        var method = contract.GetMethods().SingleOrDefault(candidate =>
            string.Equals(candidate.Name, request.Method, StringComparison.Ordinal) &&
            candidate.GetParameters().Count(parameter => parameter.ParameterType != typeof(CancellationToken)) == arguments.Length);
        if (method is null) throw new RpcException(new Status(StatusCode.Unimplemented, "Unknown wallet operation."));

        var parameters = method.GetParameters();
        var values = new object?[parameters.Length];
        var argumentIndex = 0;
        for (var index = 0; index < parameters.Length; index++)
            values[index] = parameters[index].ParameterType == typeof(CancellationToken)
                ? context.CancellationToken
                : JsonSerializer.Deserialize(arguments[argumentIndex++].GetRawText(), parameters[index].ParameterType, WalletWireCodec.Options);

        var task = (Task)(method.Invoke(services.GetRequiredService(contract), values)
            ?? throw new RpcException(new Status(StatusCode.Internal, "Wallet operation returned no task.")));
        await task.ConfigureAwait(false);
        var result = task.GetType().GetProperty("Result")?.GetValue(task);
        return new WalletReply { ResultJson = JsonSerializer.Serialize(result, result?.GetType() ?? typeof(object), WalletWireCodec.Options) };
    }
}

public class WalletGrpcProxy<TContract> : DispatchProxy where TContract : class
{
    private WalletApi.WalletApiClient _client = null!;
    public static TContract Create(WalletApi.WalletApiClient client)
    {
        var proxy = Create<TContract, WalletGrpcProxy<TContract>>();
        ((WalletGrpcProxy<TContract>)(object)proxy)._client = client;
        return proxy;
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        ArgumentNullException.ThrowIfNull(targetMethod);
        var parameters = targetMethod.GetParameters();
        var payload = (args ?? []).Where((_, index) => parameters[index].ParameterType != typeof(CancellationToken)).ToArray();
        var ct = (args ?? []).OfType<CancellationToken>().FirstOrDefault();
        var call = new WalletCall
        {
            Contract = typeof(TContract).FullName!, Method = targetMethod.Name,
            ArgumentsJson = JsonSerializer.Serialize(payload, WalletWireCodec.Options),
        };
        if (targetMethod.ReturnType == typeof(Task)) return InvokeVoidAsync(call, ct);
        return typeof(WalletGrpcProxy<TContract>).GetMethod(nameof(InvokeAsync), BindingFlags.Instance | BindingFlags.NonPublic)!
            .MakeGenericMethod(targetMethod.ReturnType.GetGenericArguments().Single()).Invoke(this, [call, ct]);
    }

    private async Task InvokeVoidAsync(WalletCall call, CancellationToken ct) =>
        _ = await _client.InvokeAsync(call, cancellationToken: ct);
    private async Task<TResult> InvokeAsync<TResult>(WalletCall call, CancellationToken ct) =>
        JsonSerializer.Deserialize<TResult>((await _client.InvokeAsync(call, cancellationToken: ct)).ResultJson, WalletWireCodec.Options)!;
}

public static class WalletGrpcExtensions
{
    public static IServiceCollection AddWalletGrpcClients(this IServiceCollection services, Uri address)
    {
        services.AddResilientGrpcClient<WalletApi.WalletApiClient>(address);
        services.AddSingleton(provider => WalletGrpcProxy<IEconomicsService>.Create(provider.GetRequiredService<WalletApi.WalletApiClient>()));
        services.AddSingleton(provider => WalletGrpcProxy<IWalletAtomicExecutionService>.Create(provider.GetRequiredService<WalletApi.WalletApiClient>()));
        services.AddSingleton(provider => WalletGrpcProxy<IDailyBonusService>.Create(provider.GetRequiredService<WalletApi.WalletApiClient>()));
        services.AddSingleton(provider => WalletGrpcProxy<IWalletReadService>.Create(provider.GetRequiredService<WalletApi.WalletApiClient>()));
        services.AddSingleton(provider => WalletGrpcProxy<IWalletAnalyticsService>.Create(provider.GetRequiredService<WalletApi.WalletApiClient>()));
        services.AddSingleton(provider => WalletGrpcProxy<IPlayerProtectionService>.Create(provider.GetRequiredService<WalletApi.WalletApiClient>()));
        return services;
    }

    public static IEndpointRouteBuilder MapWalletGrpcTransport(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGrpcService<WalletGrpcEndpoint>();
        return endpoints;
    }
}
