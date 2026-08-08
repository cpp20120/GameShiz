using System.Text.Json;
using BotFramework.Host.Contracts.Economics;
using BotFramework.Host.Contracts.ResponsibleGaming;
using CasinoShiz.Wallet.Transport.Grpc.Wire;
using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;

namespace CasinoShiz.Wallet.Transport.Grpc;

public sealed class WalletGrpcEndpoint(IServiceProvider services) : WalletApi.WalletApiBase
{
    private static readonly Dictionary<string, Type> Contracts = new[]
    {
        typeof(IEconomicsService), typeof(IWalletAtomicExecutionService), typeof(IWalletSnapshotService), typeof(IDailyBonusService), typeof(IWalletReadService), typeof(IWalletAnalyticsService), typeof(IPlayerProtectionService),
    }.ToDictionary(type => type.FullName!, StringComparer.Ordinal);

    public override async Task<WalletReply> Invoke(WalletCall request, ServerCallContext context)
    {
        if (!Contracts.TryGetValue(request.Contract, out var contract))
            throw new RpcException(new Status(StatusCode.Unimplemented, "Unknown wallet contract."));
        var arguments = JsonSerializer.Deserialize<JsonElement[]>(request.ArgumentsJson, WalletWireCodec.Options) ?? [];
        var method = contract.GetMethods().SingleOrDefault(candidate =>
            string.Equals(candidate.Name, request.Method, StringComparison.Ordinal) &&
            candidate.GetParameters().Count(parameter => parameter.ParameterType != typeof(CancellationToken)) == arguments.Length);
        if (method is not null)
        {
            var parameters = method.GetParameters();
            var values = new object?[parameters.Length];
            var argumentIndex = 0;
            for (var index = 0; index < parameters.Length; index++)
            {
                values[index] = parameters[index].ParameterType == typeof(CancellationToken)
                    ? context.CancellationToken
                    : JsonSerializer.Deserialize(arguments[argumentIndex++].GetRawText(),
                        parameters[index].ParameterType, WalletWireCodec.Options);
            }

            var task = (Task)(method.Invoke(services.GetRequiredService(contract), values)
                              ?? throw new RpcException(new Status(StatusCode.Internal,
                                  "Wallet operation returned no task.")));
            await task;
            var result = task.GetType().GetProperty("Result")?.GetValue(task);
            return new WalletReply
            {
                ResultJson = JsonSerializer.Serialize(result, result?.GetType() ?? typeof(object),
                    WalletWireCodec.Options),
            };
        }

        throw new RpcException(new Status(StatusCode.Unimplemented, "Unknown wallet operation."));
    }
}
