using BotFramework.Sdk.Execution;
using Games.Challenges.Domain.Events;

namespace Games.Challenges.Application.Execution;

public sealed class ChallengeCreateAction
    : IGameAction<ChallengeCreateCommand, ChallengeExecutionState, ChallengeCreateResult>
{
    public GameDecision<ChallengeExecutionState, ChallengeCreateResult> Decide(
        GameActionInput<ChallengeExecutionState, ChallengeCreateCommand> input)
    {
        var command = input.Command;
        if (command.Target.UserId == command.ActorUserId)
            return Reject(input.State, ChallengeCreateError.SelfChallenge);
        if (command.Amount < command.MinBet || command.Amount > command.MaxBet)
            return Reject(input.State, ChallengeCreateError.InvalidAmount);
        if (input.Wallet.Balance < command.Amount)
            return Reject(input.State, ChallengeCreateError.NotEnoughCoins, checked((int)input.Wallet.Balance));
        if (input.State.HasPendingPair)
            return Reject(input.State, ChallengeCreateError.AlreadyPending);

        var challenge = new Challenge(command.ChallengeId, command.ChatId, command.ActorUserId,
            command.DisplayName, command.Target.UserId, command.Target.DisplayName, command.Amount,
            command.Game, ChallengeStatus.Pending, input.UtcNow, input.UtcNow.Add(command.PendingTtl));
        return new(DecisionStatus.Accepted, input.State with { Challenge = challenge },
            new(ChallengeCreateError.None, challenge), [], [], [],
            [new ChallengeCreated(challenge.Id, challenge.ChatId, challenge.ChallengerId,
                challenge.TargetId, challenge.Amount, challenge.Game.ToString(),
                input.UtcNow.ToUnixTimeMilliseconds())], []);
    }

    private static GameDecision<ChallengeExecutionState, ChallengeCreateResult> Reject(
        ChallengeExecutionState state, ChallengeCreateError error, int balance = 0) =>
        new(DecisionStatus.Rejected, state, new(error, Balance: balance), [], [], [], [], [], error.ToString());
}
