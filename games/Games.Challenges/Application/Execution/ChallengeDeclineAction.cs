using BotFramework.Sdk.Execution;
using Games.Challenges.Domain.Events;

namespace Games.Challenges.Application.Execution;

public sealed class ChallengeDeclineAction
    : IGameAction<ChallengeDeclineCommand, ChallengeExecutionState, ChallengeAcceptError>
{
    public GameDecision<ChallengeExecutionState, ChallengeAcceptError> Decide(
        GameActionInput<ChallengeExecutionState, ChallengeDeclineCommand> input)
    {
        if (input.State.Challenge is not { } challenge) return Reject(input.State, ChallengeAcceptError.NotFound);
        if (challenge.TargetId != input.Command.ActorUserId) return Reject(input.State, ChallengeAcceptError.NotTarget);
        if (challenge.Status != ChallengeStatus.Pending) return Reject(input.State, ChallengeAcceptError.AlreadyResolved);
        var declined = challenge with { Status = ChallengeStatus.Declined };
        return new(DecisionStatus.Accepted, input.State with { Challenge = declined }, ChallengeAcceptError.None,
            [], [], [], [new ChallengeStatusChanged(challenge.Id, challenge.ChatId, "declined",
                input.UtcNow.ToUnixTimeMilliseconds())], []);
    }

    private static GameDecision<ChallengeExecutionState, ChallengeAcceptError> Reject(
        ChallengeExecutionState state, ChallengeAcceptError error) =>
        new(DecisionStatus.Rejected, state, error, [], [], [], [], [], error.ToString());
}
