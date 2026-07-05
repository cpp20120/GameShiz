using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BotFramework.Contracts.Operations;
using CasinoShiz.Operations.Transport.Grpc.Wire;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
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

        var method=typeof(IOperationsAdminService).GetMethods().SingleOrDefault(x=>x.Name==request.Method)
            ?? throw new RpcException(new Status(StatusCode.Unimplemented,"Unknown operation."));
        var args=JsonSerializer.Deserialize<JsonElement[]>(request.ArgumentsJson,Json)??[];
        var parameters=method.GetParameters(); var values=new object?[parameters.Length]; var ai=0;
        for(var i=0;i<parameters.Length;i++) values[i]=parameters[i].ParameterType==typeof(CancellationToken)
            ? context.CancellationToken : JsonSerializer.Deserialize(args[ai++].GetRawText(),parameters[i].ParameterType,Json);
        var task=(Task)(method.Invoke(service,values)??throw new RpcException(new Status(StatusCode.Internal,"No task.")));
        await task.ConfigureAwait(false); var result=task.GetType().GetProperty("Result")?.GetValue(task);
        return new(){ResultJson=JsonSerializer.Serialize(result,result?.GetType()??typeof(object),Json)};
    }

    private static bool FixedEquals(string left, string right) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(left), Encoding.UTF8.GetBytes(right));
}

public class OperationsGrpcProxy : DispatchProxy
{
    private OperationsApi.OperationsApiClient _client=null!;
    private string _apiKey = null!;
    public static IOperationsAdminService Create(OperationsApi.OperationsApiClient client, string apiKey){var p=Create<IOperationsAdminService,OperationsGrpcProxy>();var proxy=(OperationsGrpcProxy)(object)p;proxy._client=client;proxy._apiKey=apiKey;return p;}
    protected override object? Invoke(MethodInfo? method,object?[]? args)
    {
        ArgumentNullException.ThrowIfNull(method); var ps=method.GetParameters();
        var payload=(args??[]).Where((_,i)=>ps[i].ParameterType!=typeof(CancellationToken)).ToArray();
        var ct=(args??[]).OfType<CancellationToken>().FirstOrDefault();
        var call=new OperationsCall{Method=method.Name,ArgumentsJson=JsonSerializer.Serialize(payload,new JsonSerializerOptions(JsonSerializerDefaults.Web))};
        var resultType=method.ReturnType.GetGenericArguments().Single();
        return typeof(OperationsGrpcProxy).GetMethod(nameof(Call),BindingFlags.Instance|BindingFlags.NonPublic)!.MakeGenericMethod(resultType).Invoke(this,[call,ct]);
    }
    private async Task<T> Call<T>(OperationsCall call,CancellationToken ct)
    {
        var headers = new Metadata { { "x-admin-api-key", _apiKey } };
        return JsonSerializer.Deserialize<T>((await _client.InvokeAsync(call, headers, cancellationToken:ct)).ResultJson,new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
    }
}

public static class OperationsGrpcExtensions
{
    public static IServiceCollection AddOperationsGrpcClient(this IServiceCollection services,Uri address,string apiKey){services.AddSingleton(_=>new OperationsApi.OperationsApiClient(GrpcChannel.ForAddress(address)));services.AddScoped<IOperationsAdminService>(p=>OperationsGrpcProxy.Create(p.GetRequiredService<OperationsApi.OperationsApiClient>(),apiKey));return services;}
    public static IEndpointRouteBuilder MapOperationsGrpcTransport(this IEndpointRouteBuilder endpoints){endpoints.MapGrpcService<OperationsGrpcEndpoint>();return endpoints;}
}
