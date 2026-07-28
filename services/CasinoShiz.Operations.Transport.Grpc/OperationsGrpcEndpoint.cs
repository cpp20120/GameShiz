using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BotFramework.Contracts.Operations;
using CasinoShiz.Operations.Transport.Grpc.Wire;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Configuration;

namespace CasinoShiz.Operations.Transport.Grpc;

public sealed class OperationsGrpcEndpoint(IOperationsAdminService service, IConfiguration configuration) : OperationsApi.OperationsApiBase
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public override async Task<OperationsReply> Invoke(OperationsCall request, ServerCallContext context)
    {
        var expectedKey = configuration["Services:Operations:ApiKey"];
        var suppliedKey = context.RequestHeaders.GetValue("x-admin-api-key");
        if (string.IsNullOrWhiteSpace(expectedKey) || string.IsNullOrWhiteSpace(suppliedKey)
            || !FixedEquals(expectedKey, suppliedKey))
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Operations service credentials are invalid."));

        var method = typeof(IOperationsAdminService).GetMethods().SingleOrDefault(x =>
            string.Equals(x.Name, request.Method, StringComparison.Ordinal))
            ?? throw new RpcException(new Status(StatusCode.Unimplemented,"Unknown operation."));
        var args=JsonSerializer.Deserialize<JsonElement[]>(request.ArgumentsJson,Json)??[];
        var parameters=method.GetParameters(); var values=new object?[parameters.Length]; var ai=0;
        for(var i=0;i<parameters.Length;i++) values[i]=parameters[i].ParameterType==typeof(CancellationToken)
            ? context.CancellationToken : JsonSerializer.Deserialize(args[ai++].GetRawText(),parameters[i].ParameterType,Json);
        var task=(Task)(method.Invoke(service,values)??throw new RpcException(new Status(StatusCode.Internal,"No task.")));
        await task; var result=task.GetType().GetProperty("Result")?.GetValue(task);
        return new(){ResultJson=JsonSerializer.Serialize(result,result?.GetType()??typeof(object),Json)};
    }

    private static bool FixedEquals(string left, string right) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(left), Encoding.UTF8.GetBytes(right));
}
