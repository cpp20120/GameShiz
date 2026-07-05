using System.Text.Json;
using Games.SecretHitler.Application.Services;
using Games.SecretHitler.Domain.Entities;
using Games.SecretHitler.Domain.Results;
using Games.SecretHitler.Transport.Grpc.Wire;
using Grpc.Core;

namespace Games.SecretHitler.Transport.Grpc;

internal static class SecretHitlerWire
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
    public static ContractCall Call<T>(T value) => new() { PayloadJson = JsonSerializer.Serialize(value, Options) };
    public static ContractReply Reply<T>(T value) => new() { PayloadJson = JsonSerializer.Serialize(value, Options) };
    public static T Read<T>(this ContractCall value) => JsonSerializer.Deserialize<T>(value.PayloadJson, Options)!;
    public static T Read<T>(this ContractReply value) => JsonSerializer.Deserialize<T>(value.PayloadJson, Options)!;
}

internal sealed record ShUserCall(long UserId, int Value = 0);
internal sealed record ShCreateCall(long UserId, string DisplayName, long PublicChatId, long PlayerChatId);
internal sealed record ShJoinCall(long UserId, string DisplayName, long PlayerChatId, string Code);
internal sealed record ShVoteCall(long UserId, ShVote Vote);
internal sealed record ShPublicMessageCall(string Code, int MessageId);
internal sealed record ShGameReply(ShGameSnapshot? Snapshot, SecretHitlerPlayer? Player);
internal sealed record ShEmptyReply;

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
        return SecretHitlerWire.Reply(new ShEmptyReply());
    }
    public override async Task<ContractReply> SetPublicStateMessage(ContractCall request, ServerCallContext context)
    {
        var call = request.Read<ShPublicMessageCall>();
        await service.SetPublicStateMessageIdAsync(call.Code, call.MessageId, context.CancellationToken);
        return SecretHitlerWire.Reply(new ShEmptyReply());
    }
}

public sealed class GrpcSecretHitlerService(SecretHitlerApi.SecretHitlerApiClient client) : ISecretHitlerService
{
    public async Task<(ShGameSnapshot? Snapshot, SecretHitlerPlayer? Me)> FindMyGameAsync(long userId, CancellationToken ct)
    {
        var reply = (await client.FindMyGameAsync(SecretHitlerWire.Call(new ShUserCall(userId)), cancellationToken: ct)).Read<ShGameReply>();
        return (reply.Snapshot, reply.Player);
    }
    public async Task<ShCreateResult> CreateGameAsync(long userId, string displayName, long publicChatId, long playerChatId, CancellationToken ct) =>
        (await client.CreateGameAsync(SecretHitlerWire.Call(new ShCreateCall(userId, displayName, publicChatId, playerChatId)), cancellationToken: ct)).Read<ShCreateResult>();
    public async Task<ShJoinResult> JoinGameAsync(long userId, string displayName, long playerChatId, string code, CancellationToken ct) =>
        (await client.JoinGameAsync(SecretHitlerWire.Call(new ShJoinCall(userId, displayName, playerChatId, code)), cancellationToken: ct)).Read<ShJoinResult>();
    public async Task<ShStartResult> StartGameAsync(long userId, CancellationToken ct) =>
        (await client.StartGameAsync(SecretHitlerWire.Call(new ShUserCall(userId)), cancellationToken: ct)).Read<ShStartResult>();
    public async Task<ShNominateResult> NominateAsync(long userId, int chancellorPosition, CancellationToken ct) =>
        (await client.NominateAsync(SecretHitlerWire.Call(new ShUserCall(userId, chancellorPosition)), cancellationToken: ct)).Read<ShNominateResult>();
    public async Task<ShVoteResult> VoteAsync(long userId, ShVote vote, CancellationToken ct) =>
        (await client.VoteAsync(SecretHitlerWire.Call(new ShVoteCall(userId, vote)), cancellationToken: ct)).Read<ShVoteResult>();
    public async Task<ShDiscardResult> PresidentDiscardAsync(long userId, int discardIndex, CancellationToken ct) =>
        (await client.PresidentDiscardAsync(SecretHitlerWire.Call(new ShUserCall(userId, discardIndex)), cancellationToken: ct)).Read<ShDiscardResult>();
    public async Task<ShEnactResult> ChancellorEnactAsync(long userId, int enactIndex, CancellationToken ct) =>
        (await client.ChancellorEnactAsync(SecretHitlerWire.Call(new ShUserCall(userId, enactIndex)), cancellationToken: ct)).Read<ShEnactResult>();
    public async Task<ShLeaveResult> LeaveAsync(long userId, CancellationToken ct) =>
        (await client.LeaveAsync(SecretHitlerWire.Call(new ShUserCall(userId)), cancellationToken: ct)).Read<ShLeaveResult>();
    public async Task SetStateMessageIdAsync(long userId, int messageId, CancellationToken ct) =>
        _ = await client.SetStateMessageAsync(SecretHitlerWire.Call(new ShUserCall(userId, messageId)), cancellationToken: ct);
    public async Task SetPublicStateMessageIdAsync(string inviteCode, int messageId, CancellationToken ct) =>
        _ = await client.SetPublicStateMessageAsync(SecretHitlerWire.Call(new ShPublicMessageCall(inviteCode, messageId)), cancellationToken: ct);
}
