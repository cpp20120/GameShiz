using Games.Challenges.Application.Services;
using Games.Challenges.Domain.Entities;
using Games.Challenges.Domain.Results;
using Games.Challenges.Transport.Grpc.Wire;

namespace Games.Challenges.Transport.Grpc;

public sealed class GrpcChallengeService(ChallengeApi.ChallengeApiClient client) : IChallengeService
{
    public async Task<ChallengeUser?> FindKnownUserByUsernameAsync(
        long chatId, string username, CancellationToken ct) =>
        (await client.FindUserAsync(
            ChallengeWireCodec.Call(new FindUserCall(chatId, username)),
            cancellationToken: ct)).Read<ChallengeUser?>();

    public async Task<ChallengeCreateResult> CreateAsync(
        long challengerId,
        string challengerName,
        ChallengeUser target,
        long chatId,
        int amount,
        ChallengeGame game,
        CancellationToken ct) =>
        (await client.CreateAsync(
            ChallengeWireCodec.Call(new CreateChallengeCall(
                challengerId, challengerName, target, chatId, amount, game)),
            cancellationToken: ct)).Read<ChallengeCreateResult>();

    public async Task<ChallengeAcceptResult> BeginAcceptAsync(
        Guid challengeId, long actorId, CancellationToken ct) =>
        (await client.BeginAcceptAsync(
            ChallengeWireCodec.Call(new ChallengeActorCall(challengeId, actorId)),
            cancellationToken: ct)).Read<ChallengeAcceptResult>();

    public async Task<ChallengeAcceptError> DeclineAsync(
        Guid challengeId, long actorId, CancellationToken ct) =>
        (await client.DeclineAsync(
            ChallengeWireCodec.Call(new ChallengeActorCall(challengeId, actorId)),
            cancellationToken: ct)).Read<ChallengeAcceptError>();

    public async Task<ChallengeAcceptResult> CompleteAcceptedAsync(
        Challenge challenge, int challengerRoll, int targetRoll, CancellationToken ct) =>
        (await client.CompleteAsync(
            ChallengeWireCodec.Call(new CompleteChallengeCall(challenge, challengerRoll, targetRoll)),
            cancellationToken: ct)).Read<ChallengeAcceptResult>();

    public async Task FailAcceptedAsync(Challenge challenge, CancellationToken ct) =>
        _ = await client.FailAsync(
            ChallengeWireCodec.Call(new ChallengeCall(challenge)),
            cancellationToken: ct);
}
