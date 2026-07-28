using System.Text.Json;
using Games.SecretHitler.Application.Services;
using Games.SecretHitler.Domain.Entities;
using Games.SecretHitler.Domain.Results;
using Games.SecretHitler.Transport.Grpc.Wire;
using Grpc.Core;

namespace Games.SecretHitler.Transport.Grpc;

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
