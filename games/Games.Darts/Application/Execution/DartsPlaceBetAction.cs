using BotFramework.Contracts.Messaging;
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
            DartsRoundStatus.Queued, null, command.ReplyToMessageId, input.Channel);
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
