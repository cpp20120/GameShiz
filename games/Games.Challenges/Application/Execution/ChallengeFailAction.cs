using BotFramework.Sdk.Execution;
using Games.Challenges.Domain.Events;

namespace Games.Challenges.Application.Execution;

public sealed class ChallengeFailAction
    : IGameAction<ChallengeFailCommand, ChallengeExecutionState, bool>
{
    public GameDecision<ChallengeExecutionState, bool> Decide(
        GameActionInput<ChallengeExecutionState, ChallengeFailCommand> input)
    {
        if (input.State.Challenge is not { Status: ChallengeStatus.Accepted } challenge)
            return new(DecisionStatus.Rejected, input.State, false, [], [], [], [], [], "not_accepted");
        var failed = challenge with { Status = ChallengeStatus.Failed };
        return new(DecisionStatus.Accepted, input.State with { Challenge = failed }, true, [], [], [],
            [new ChallengeStatusChanged(challenge.Id, challenge.ChatId, "failed",
                input.UtcNow.ToUnixTimeMilliseconds())], [],
            CustomEffects:
            [
                WalletEconomyEffect.Credit(challenge.ChallengerId, challenge.ChatId, challenge.Amount, "challenge.refund"),
                WalletEconomyEffect.Credit(challenge.TargetId, challenge.ChatId, challenge.Amount, "challenge.refund"),
            ]);
    }
}
