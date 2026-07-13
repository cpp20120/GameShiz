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
        var winnerId = tie ? 0 : input.Command.ChallengerRoll > input.Command.TargetRoll
            ? challenge.ChallengerId : challenge.TargetId;
        var winnerName = winnerId == challenge.ChallengerId ? challenge.ChallengerName
            : winnerId == challenge.TargetId ? challenge.TargetName : "";
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
