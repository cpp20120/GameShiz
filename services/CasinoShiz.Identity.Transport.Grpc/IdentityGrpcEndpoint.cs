using BotFramework.Contracts.Identity;
using CasinoShiz.Identity.Transport.Grpc.Wire;
using Grpc.Core;

namespace CasinoShiz.Identity.Transport.Grpc;

public sealed class IdentityGrpcEndpoint(IPlayerDirectory directory) : IdentityApi.IdentityApiBase
{
    public override async Task<IdentityReply> Upsert(IdentityCall request, ServerCallContext context)
    {
        await directory.UpsertAsync(request.Read<PlayerIdentity>(), context.CancellationToken);
        return IdentityWireCodec.Reply(EmptyReply.Create());
    }

    public override async Task<IdentityReply> Get(IdentityCall request, ServerCallContext context) =>
        IdentityWireCodec.Reply(await directory.GetAsync(request.Read<UserIdCall>().UserId, context.CancellationToken));

    public override async Task<IdentityReply> FindByUsername(IdentityCall request, ServerCallContext context) =>
        IdentityWireCodec.Reply(await directory.FindByUsernameAsync(request.Read<UsernameCall>().Username, context.CancellationToken));
}
