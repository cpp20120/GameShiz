using BotFramework.Contracts.Identity;
using CasinoShiz.Identity.Transport.Grpc.Wire;

namespace CasinoShiz.Identity.Transport.Grpc;

public sealed class GrpcPlayerDirectory(IdentityApi.IdentityApiClient client) : IPlayerDirectory
{
    public async Task UpsertAsync(PlayerIdentity identity, CancellationToken ct) =>
        _ = await client.UpsertAsync(IdentityWireCodec.Call(identity), cancellationToken: ct);
    public async Task<PlayerIdentity?> GetAsync(long userId, CancellationToken ct) =>
        (await client.GetAsync(IdentityWireCodec.Call(new UserIdCall(userId)), cancellationToken: ct)).Read<PlayerIdentity?>();
    public async Task<PlayerIdentity?> FindByUsernameAsync(string username, CancellationToken ct) =>
        (await client.FindByUsernameAsync(IdentityWireCodec.Call(new UsernameCall(username)), cancellationToken: ct)).Read<PlayerIdentity?>();
}