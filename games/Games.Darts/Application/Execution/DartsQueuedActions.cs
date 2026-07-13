using BotFramework.Sdk.Execution;

namespace Games.Darts.Application.Execution;

public sealed class DartsPlaceBetAction
    : IGameAction<DartsPlaceBetCommand, DartsQueuedState, DartsBetResult>
{
    public const string DailyRollQuota = "darts.daily-roll";

    public GameDecision<DartsQueuedState, DartsBetResult> Decide(
        GameActionInput<DartsQueuedState, DartsPlaceBetCommand> input)
    {
        var command = input.Command;
        var balance = checked((int)input.Wallet.Balance);
        if (command.Amount <= 0 || command.Amount > command.MaxBet)
            return Reject(input.State, DartsBetResult.Fail(DartsBetError.InvalidAmount), "invalid_amount");
        if (command.BlockingGameId is not null)
        {
            return Reject(input.State,
                new DartsBetResult(DartsBetError.BusyOtherGame, Balance: balance,
                    BlockingGameId: command.BlockingGameId), "busy_other_game");
        }
        var quota = RequiredQuota(input.Quotas);
        if (quota.Limit > 0 && quota.Used >= quota.Limit)
        {
            return Reject(input.State,
                new DartsBetResult(DartsBetError.DailyRollLimit, Balance: balance,
                    DailyRollUsed: checked((int)quota.Used), DailyRollLimit: checked((int)quota.Limit)),
                "daily_roll_limit");
        }
        if (command.Amount > input.Wallet.Balance)
            return Reject(input.State, DartsBetResult.Fail(DartsBetError.NotEnoughCoins, balance), "insufficient_balance");

        var round = new DartsRound(
            command.RoundId, command.UserId, command.ChatId, command.Amount, input.UtcNow,
            DartsRoundStatus.Queued, null, command.ReplyToMessageId);
        return new(
            DecisionStatus.Accepted,
            new DartsQueuedState(round, input.State.QueuedAhead),
            new DartsBetResult(
                DartsBetError.None, command.Amount, balance - command.Amount, RoundId: round.Id,
                QueuedAhead: input.State.QueuedAhead,
                DailyRollUsed: quota.Limit > 0 ? checked((int)quota.Used + 1) : 0,
                DailyRollLimit: checked((int)quota.Limit)),
            [EconomyEffect.Debit(command.Amount, "darts.bet")],
            quota.Limit > 0 ? [QuotaEffect.Consume(DailyRollQuota)] : [],
            [],
            [new DartsBetPlaced(command.UserId, command.ChatId, command.Amount, round.Id,
                input.UtcNow.ToUnixTimeMilliseconds())],
            []);
    }

    internal static QuotaSnapshot RequiredQuota(IReadOnlyDictionary<string, QuotaSnapshot> quotas) =>
        quotas.TryGetValue(DailyRollQuota, out var quota)
            ? quota
            : throw new InvalidOperationException($"Required quota '{DailyRollQuota}' was not supplied.");

    private static GameDecision<DartsQueuedState, DartsBetResult> Reject(
        DartsQueuedState state, DartsBetResult result, string reason) =>
        new(DecisionStatus.Rejected, state, result, [], [], [], [], [], reason);
}

public sealed class DartsResolveRoundAction
    : IGameAction<DartsResolveRoundCommand, DartsQueuedState, DartsThrowResult>
{
    public const string RedeemDropEntropy = "redeem-drop";

    public GameDecision<DartsQueuedState, DartsThrowResult> Decide(
        GameActionInput<DartsQueuedState, DartsResolveRoundCommand> input)
    {
        var command = input.Command;
        if (input.State.Round is not { } round
            || round.Status != DartsRoundStatus.AwaitingOutcome
            || round.UserId != command.UserId
            || round.ChatId != command.ChatId
            || round.BotMessageId != command.BotDiceMessageId)
        {
            return new(DecisionStatus.Rejected, input.State,
                new DartsThrowResult(DartsThrowOutcome.NoBet), [], [], [], [], [], "no_matching_round");
        }

        var quota = DartsPlaceBetAction.RequiredQuota(input.Quotas);
        var multiplier = DartsRules.Multiplier(command.Face);
        var payout = checked(round.Amount * multiplier);
        var occurredAt = input.UtcNow.ToUnixTimeMilliseconds();
        var events = new List<IDomainEvent>
        {
            new DartsThrowCompleted(command.UserId, command.ChatId, command.Face, round.Amount,
                multiplier, payout, occurredAt),
            new GameCompletedMetaEvent(command.ChatId, command.UserId, command.DisplayName,
                MiniGameIds.Darts, round.Amount, payout, payout > round.Amount,
                decimal.Divide(payout, round.Amount), occurredAt),
        };
        if (command.RedeemDropChance > 0
            && input.Entropy.GetDouble(RedeemDropEntropy) < command.RedeemDropChance)
        {
            events.Add(new TelegramMiniGameRedeemCodeDropRequested(
                command.UserId, command.ChatId, MiniGameIds.Darts, occurredAt));
        }

        return new(
            DecisionStatus.Accepted,
            new DartsQueuedState(null, 0),
            new DartsThrowResult(DartsThrowOutcome.Thrown, command.Face, round.Amount, multiplier,
                payout, checked((int)input.Wallet.Balance + payout),
                DailyRollUsed: quota.Limit > 0 ? checked((int)quota.Used) : 0,
                DailyRollLimit: checked((int)quota.Limit)),
            payout > 0 ? [EconomyEffect.Credit(payout, "darts.payout")] : [],
            [], [], events, []);
    }
}

public sealed class DartsAbortRoundAction
    : IGameAction<DartsAbortRoundCommand, DartsQueuedState, DartsAbortRoundResult>
{
    public GameDecision<DartsQueuedState, DartsAbortRoundResult> Decide(
        GameActionInput<DartsQueuedState, DartsAbortRoundCommand> input)
    {
        if (input.State.Round is not { Status: DartsRoundStatus.Queued } round
            || round.UserId != input.Command.UserId
            || round.ChatId != input.Command.ChatId)
        {
            return new(DecisionStatus.Rejected, input.State, new(false), [], [], [], [], [], "no_queued_round");
        }
        var quota = DartsPlaceBetAction.RequiredQuota(input.Quotas);
        return new(
            DecisionStatus.Accepted,
            new DartsQueuedState(null, 0),
            new DartsAbortRoundResult(true),
            [EconomyEffect.Credit(round.Amount, "darts.bet_reply_failed.refund")],
            quota.Limit > 0 ? [QuotaEffect.Restore(DartsPlaceBetAction.DailyRollQuota)] : [],
            [],
            [new DartsBetAborted(round.UserId, round.ChatId, round.Amount, round.Id,
                input.UtcNow.ToUnixTimeMilliseconds())],
            []);
    }
}

public static class DartsRules
{
    public static IReadOnlyDictionary<int, int> Multipliers { get; } =
        new Dictionary<int, int> { [1] = 0, [2] = 0, [3] = 0, [4] = 1, [5] = 2, [6] = 2 };

    public static int Multiplier(int face) => Multipliers.GetValueOrDefault(face);
}
