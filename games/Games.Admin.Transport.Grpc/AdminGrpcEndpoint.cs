using System.Reflection;
using System.Text.Json;
using BotFramework.Host.Analytics.Reports;
using Games.Admin.Application.Services;
using Games.Admin.Infrastructure.Persistence;
using Games.Admin.Transport.Grpc.Wire;
using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Games.Admin.Transport.Grpc;

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

        await task;
        var result = task.GetType().GetProperty("Result")?.GetValue(task);
        return new AdminReply { ResultJson = JsonSerializer.Serialize(result, result?.GetType() ?? typeof(object), AdminWire.Options) };
    }
}
