using System.Reflection;
using System.Text.Json;
using CasinoShiz.Wallet.Transport.Grpc.Wire;

namespace CasinoShiz.Wallet.Transport.Grpc;

internal class WalletGrpcProxy<TContract> : DispatchProxy where TContract : class
{
    private WalletApi.WalletApiClient _client = null!;

    internal void SetClient(WalletApi.WalletApiClient client) =>
        _client = client;

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        ArgumentNullException.ThrowIfNull(targetMethod);
        var parameters = targetMethod.GetParameters();
        var payload = (args ?? [])
            .Where((_, index) => parameters[index].ParameterType != typeof(CancellationToken))
            .ToArray();
        var ct = (args ?? []).OfType<CancellationToken>().FirstOrDefault();
        var call = new WalletCall
        {
            Contract = typeof(TContract).FullName!,
            Method = targetMethod.Name,
            ArgumentsJson = JsonSerializer.Serialize(payload, WalletWireCodec.Options),
        };

        if (targetMethod.ReturnType == typeof(Task))
            return InvokeVoidAsync(call, ct);

        return typeof(WalletGrpcProxy<TContract>)
            .GetMethod(nameof(InvokeAsync), BindingFlags.Instance | BindingFlags.NonPublic)!
            .MakeGenericMethod(targetMethod.ReturnType.GetGenericArguments().Single())
            .Invoke(this, [call, ct]);
    }

    private async Task InvokeVoidAsync(WalletCall call, CancellationToken ct) =>
        _ = await _client.InvokeAsync(call, cancellationToken: ct);

    private async Task<TResult> InvokeAsync<TResult>(WalletCall call, CancellationToken ct) =>
        JsonSerializer.Deserialize<TResult>(
            (await _client.InvokeAsync(call, cancellationToken: ct)).ResultJson,
            WalletWireCodec.Options)!;
}
