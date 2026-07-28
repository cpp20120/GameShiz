using System.Text.Json;
using Games.SecretHitler.Application.Services;
using Games.SecretHitler.Domain.Entities;
using Games.SecretHitler.Domain.Results;
using Games.SecretHitler.Transport.Grpc.Wire;
using Grpc.Core;

namespace Games.SecretHitler.Transport.Grpc;

public sealed class SecretHitlerGrpcEndpoint(ISecretHitlerService service) : SecretHitlerApi.SecretHitlerApiBase
{
    public override async Task<ContractReply> FindMyGame(ContractCall request, ServerCallContext context)
    {
        var (snapshot, player) = await service.FindMyGameAsync(request.Read<ShUserCall>().UserId, context.CancellationToken);
        return SecretHitlerWire.Reply(new ShGameReply(snapshot, player));
    }

    public override async Task<ContractReply> CreateGame(ContractCall request, ServerCallContext context)
    {
        var call = request.Read<ShCreateCall>();
        return SecretHitlerWire.Reply(await service.CreateGameAsync(call.UserId, call.DisplayName, call.PublicChatId, call.PlayerChatId, context.CancellationToken));
    }

    public override async Task<ContractReply> JoinGame(ContractCall request, ServerCallContext context)
    {
        var call = request.Read<ShJoinCall>();
        return SecretHitlerWire.Reply(await service.JoinGameAsync(call.UserId, call.DisplayName, call.PlayerChatId, call.Code, context.CancellationToken));
    }

    public override async Task<ContractReply> StartGame(ContractCall request, ServerCallContext context) =>
        SecretHitlerWire.Reply(await service.StartGameAsync(request.Read<ShUserCall>().UserId, context.CancellationToken));
    public override async Task<ContractReply> Nominate(ContractCall request, ServerCallContext context)
    {
        var call = request.Read<ShUserCall>();
        return SecretHitlerWire.Reply(await service.NominateAsync(call.UserId, call.Value, context.CancellationToken));
    }
    public override async Task<ContractReply> Vote(ContractCall request, ServerCallContext context)
    {
        var call = request.Read<ShVoteCall>();
        return SecretHitlerWire.Reply(await service.VoteAsync(call.UserId, call.Vote, context.CancellationToken));
    }
    public override async Task<ContractReply> PresidentDiscard(ContractCall request, ServerCallContext context)
    {
        var call = request.Read<ShUserCall>();
        return SecretHitlerWire.Reply(await service.PresidentDiscardAsync(call.UserId, call.Value, context.CancellationToken));
    }
    public override async Task<ContractReply> ChancellorEnact(ContractCall request, ServerCallContext context)
    {
        var call = request.Read<ShUserCall>();
        return SecretHitlerWire.Reply(await service.ChancellorEnactAsync(call.UserId, call.Value, context.CancellationToken));
    }
    public override async Task<ContractReply> Leave(ContractCall request, ServerCallContext context) =>
        SecretHitlerWire.Reply(await service.LeaveAsync(request.Read<ShUserCall>().UserId, context.CancellationToken));
    public override async Task<ContractReply> SetStateMessage(ContractCall request, ServerCallContext context)
    {
        var call = request.Read<ShUserCall>();
        await service.SetStateMessageIdAsync(call.UserId, call.Value, context.CancellationToken);
        return SecretHitlerWire.Reply(ShEmptyReply.Create());
    }
    public override async Task<ContractReply> SetPublicStateMessage(ContractCall request, ServerCallContext context)
    {
        var call = request.Read<ShPublicMessageCall>();
        await service.SetPublicStateMessageIdAsync(call.Code, call.MessageId, context.CancellationToken);
        return SecretHitlerWire.Reply(ShEmptyReply.Create());
    }
}
