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

public sealed class MetaGrpcEndpoint(IServiceProvider services) : MetaApi.MetaApiBase
{
    private static readonly Dictionary<string, Type> Contracts = new[]
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
            string.Equals(candidate.Name, request.Method, StringComparison.Ordinal) &&
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
        await task;
        var result = task.GetType().GetProperty("Result")?.GetValue(task);
        return new MetaReply { ResultJson = JsonSerializer.Serialize(result, result?.GetType() ?? typeof(object), MetaWire.Options) };
    }
}
