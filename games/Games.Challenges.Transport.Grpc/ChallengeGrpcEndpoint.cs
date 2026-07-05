using Games.Challenges.Application.Services;
using Games.Challenges.Transport.Grpc.Wire;
using Grpc.Core;

namespace Games.Challenges.Transport.Grpc;

public sealed class ChallengeGrpcEndpoint(IChallengeService service) : ChallengeApi.ChallengeApiBase
{
    public override async Task<ContractReply> FindUser(ContractCall request, ServerCallContext context)
    {
        var call = request.Read<FindUserCall>();
        return ChallengeWireCodec.Reply(await service.FindKnownUserByUsernameAsync(
            call.ChatId, call.Username, context.CancellationToken));
    }

    public override async Task<ContractReply> Create(ContractCall request, ServerCallContext context)
    {
        var call = request.Read<CreateChallengeCall>();
        return ChallengeWireCodec.Reply(await service.CreateAsync(
            call.ChallengerId,
            call.ChallengerName,
            call.Target,
            call.ChatId,
            call.Amount,
            call.Game,
            context.CancellationToken));
    }

    public override async Task<ContractReply> BeginAccept(ContractCall request, ServerCallContext context)
    {
        var call = request.Read<ChallengeActorCall>();
        return ChallengeWireCodec.Reply(await service.BeginAcceptAsync(
            call.ChallengeId, call.ActorId, context.CancellationToken));
    }

    public override async Task<ContractReply> Decline(ContractCall request, ServerCallContext context)
    {
        var call = request.Read<ChallengeActorCall>();
        return ChallengeWireCodec.Reply(await service.DeclineAsync(
            call.ChallengeId, call.ActorId, context.CancellationToken));
    }

    public override async Task<ContractReply> Complete(ContractCall request, ServerCallContext context)
    {
        var call = request.Read<CompleteChallengeCall>();
        return ChallengeWireCodec.Reply(await service.CompleteAcceptedAsync(
            call.Challenge,
            call.ChallengerRoll,
            call.TargetRoll,
            context.CancellationToken));
    }

    public override async Task<ContractReply> Fail(ContractCall request, ServerCallContext context)
    {
        await service.FailAcceptedAsync(request.Read<ChallengeCall>().Challenge, context.CancellationToken);
        return ChallengeWireCodec.Reply(true);
    }
}
