using CasinoShiz.ServiceDefaults;
using System.Text.Json;
using BotFramework.Contracts.Identity;
using CasinoShiz.Identity.Transport.Grpc.Wire;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace CasinoShiz.Identity.Transport.Grpc;

internal static class IdentityWireCodec
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
    public static IdentityCall Call<T>(T value) => new() { PayloadJson = JsonSerializer.Serialize(value, Options) };
    public static IdentityReply Reply<T>(T value) => new() { PayloadJson = JsonSerializer.Serialize(value, Options) };
    public static T Read<T>(this IdentityCall value) => JsonSerializer.Deserialize<T>(value.PayloadJson, Options)!;
    public static T Read<T>(this IdentityReply value) => JsonSerializer.Deserialize<T>(value.PayloadJson, Options)!;
}

internal sealed record UserIdCall(long UserId);
internal sealed record UsernameCall(string Username);
internal sealed record EmptyReply;

public sealed class IdentityGrpcEndpoint(IPlayerDirectory directory) : IdentityApi.IdentityApiBase
{
    public override async Task<IdentityReply> Upsert(IdentityCall request, ServerCallContext context)
    {
        await directory.UpsertAsync(request.Read<PlayerIdentity>(), context.CancellationToken);
        return IdentityWireCodec.Reply(new EmptyReply());
    }

    public override async Task<IdentityReply> Get(IdentityCall request, ServerCallContext context) =>
        IdentityWireCodec.Reply(await directory.GetAsync(request.Read<UserIdCall>().UserId, context.CancellationToken));

    public override async Task<IdentityReply> FindByUsername(IdentityCall request, ServerCallContext context) =>
        IdentityWireCodec.Reply(await directory.FindByUsernameAsync(request.Read<UsernameCall>().Username, context.CancellationToken));
}

public sealed class GrpcPlayerDirectory(IdentityApi.IdentityApiClient client) : IPlayerDirectory
{
    public async Task UpsertAsync(PlayerIdentity identity, CancellationToken ct) =>
        _ = await client.UpsertAsync(IdentityWireCodec.Call(identity), cancellationToken: ct);
    public async Task<PlayerIdentity?> GetAsync(long userId, CancellationToken ct) =>
        (await client.GetAsync(IdentityWireCodec.Call(new UserIdCall(userId)), cancellationToken: ct)).Read<PlayerIdentity?>();
    public async Task<PlayerIdentity?> FindByUsernameAsync(string username, CancellationToken ct) =>
        (await client.FindByUsernameAsync(IdentityWireCodec.Call(new UsernameCall(username)), cancellationToken: ct)).Read<PlayerIdentity?>();
}

public static class IdentityGrpcExtensions
{
    public static IServiceCollection AddIdentityGrpcClient(this IServiceCollection services, Uri address)
    {
        services.AddResilientGrpcClient<IdentityApi.IdentityApiClient>(address);
        services.AddSingleton<IPlayerDirectory, GrpcPlayerDirectory>();
        return services;
    }

    public static IEndpointRouteBuilder MapIdentityGrpcTransport(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGrpcService<IdentityGrpcEndpoint>();
        return endpoints;
    }
}
