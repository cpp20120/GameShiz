using BotFramework.Sdk.Execution;
using Games.Challenges.Domain.Events;

namespace Games.Challenges.Application.Execution;

public sealed class ChallengeCompleteAction
    : IGameAction<ChallengeCompleteCommand, ChallengeExecutionState, ChallengeAcceptResult>
{
    public GameDecision<ChallengeExecutionState, ChallengeAcceptResult> Decide(
        GameActionInput<ChallengeExecutionState, ChallengeCompleteCommand> input)
    {
        if (input.State.Challenge is not { } challenge)
            return Reject(input.State, ChallengeAcceptError.NotFound);
        if (challenge.Status != ChallengeStatus.Accepted)
            return Reject(input.State, ChallengeAcceptError.AlreadyResolved, challenge);

        var completed = challenge with { Status = ChallengeStatus.Completed };
        var tie = input.Command.ChallengerRoll == input.Command.TargetRoll;
        long winnerId;
        if (tie)
            winnerId = 0;
        else if (input.Command.ChallengerRoll > input.Command.TargetRoll)
            winnerId = challenge.ChallengerId;
        else
            winnerId = challenge.TargetId;

        var winnerName = winnerId switch
        {
            var id when id == challenge.ChallengerId => challenge.ChallengerName,
            var id when id == challenge.TargetId => challenge.TargetName,
            _ => "",
        };
        var pot = checked(challenge.Amount * 2);
        var fee = tie ? 0 : Math.Clamp(input.Command.HouseFeeBasisPoints, 0, 10_000) * pot / 10_000;
        var payout = tie ? 0 : pot - fee;
        IReadOnlyList<IGameEffect> effects = tie
            ?
            [
                WalletEconomyEffect.Credit(challenge.ChallengerId, challenge.ChatId, challenge.Amount, "challenge.tie_refund"),
                WalletEconomyEffect.Credit(challenge.TargetId, challenge.ChatId, challenge.Amount, "challenge.tie_refund"),
            ]
            : [WalletEconomyEffect.Credit(winnerId, challenge.ChatId, payout, "challenge.payout")];
        var result = new ChallengeAcceptResult(ChallengeAcceptError.None, completed,
            input.Command.ChallengerRoll, input.Command.TargetRoll, winnerId, winnerName, payout, fee, tie);
        return new(DecisionStatus.Accepted, input.State with { Challenge = completed }, result,
            [], [], [], [new ChallengeCompleted(challenge.Id, challenge.ChatId,
                input.Command.ChallengerRoll, input.Command.TargetRoll, winnerId, payout, fee, tie,
                input.UtcNow.ToUnixTimeMilliseconds())], [], CustomEffects: effects);
    }

    private static GameDecision<ChallengeExecutionState, ChallengeAcceptResult> Reject(
        ChallengeExecutionState state, ChallengeAcceptError error, Challenge? challenge = null) =>
        new(DecisionStatus.Rejected, state, new(error, challenge), [], [], [], [], [], error.ToString());
}
