using BotFramework.Sdk.Execution;
using Games.Challenges.Domain.Events;

namespace Games.Challenges.Application.Execution;

public sealed class ChallengeAcceptAction
    : IGameAction<ChallengeAcceptCommand, ChallengeExecutionState, ChallengeAcceptResult>
{
    public GameDecision<ChallengeExecutionState, ChallengeAcceptResult> Decide(
        GameActionInput<ChallengeExecutionState, ChallengeAcceptCommand> input)
    {
        if (input.State.Challenge is not { } challenge)
            return Reject(input.State, ChallengeAcceptError.NotFound);
        if (challenge.Status != ChallengeStatus.Pending)
            return Reject(input.State, ChallengeAcceptError.AlreadyResolved, challenge);
        if (challenge.TargetId != input.Command.ActorUserId)
            return Reject(input.State, ChallengeAcceptError.NotTarget, challenge);
        if (challenge.ExpiresAt <= input.UtcNow)
            return TransitionError(input, challenge, ChallengeAcceptError.Expired);
        if (input.State.ChallengerBalance < challenge.Amount)
            return TransitionError(input, challenge, ChallengeAcceptError.ChallengerNotEnoughCoins);
        if (input.State.TargetBalance < challenge.Amount)
            return TransitionError(input, challenge, ChallengeAcceptError.TargetNotEnoughCoins);

        var accepted = challenge with { Status = ChallengeStatus.Accepted };
        return new(DecisionStatus.Accepted, input.State with { Challenge = accepted },
            new(ChallengeAcceptError.None, accepted), [], [], [],
            [new ChallengeAccepted(challenge.Id, challenge.ChatId, challenge.ChallengerId,
                challenge.TargetId, challenge.Amount, input.UtcNow.ToUnixTimeMilliseconds())], [],
            CustomEffects:
            [
                WalletEconomyEffect.Debit(challenge.ChallengerId, challenge.ChatId, challenge.Amount, "challenge.stake"),
                WalletEconomyEffect.Debit(challenge.TargetId, challenge.ChatId, challenge.Amount, "challenge.stake"),
            ]);
    }

    private static GameDecision<ChallengeExecutionState, ChallengeAcceptResult> TransitionError(
        GameActionInput<ChallengeExecutionState, ChallengeAcceptCommand> input,
        Challenge challenge,
        ChallengeAcceptError error)
    {
        var failed = challenge with { Status = ChallengeStatus.Failed };
        return new(DecisionStatus.Accepted, input.State with { Challenge = failed },
            new(error, failed), [], [], [],
            [new ChallengeStatusChanged(challenge.Id, challenge.ChatId, "failed",
                input.UtcNow.ToUnixTimeMilliseconds())], []);
    }

    private static GameDecision<ChallengeExecutionState, ChallengeAcceptResult> Reject(
        ChallengeExecutionState state, ChallengeAcceptError error, Challenge? challenge = null) =>
        new(DecisionStatus.Rejected, state, new(error, challenge), [], [], [], [], [], error.ToString());
}
