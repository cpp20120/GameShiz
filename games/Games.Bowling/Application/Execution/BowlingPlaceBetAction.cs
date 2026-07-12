using BotFramework.Sdk.Execution;

namespace Games.Bowling.Application.Execution;

public sealed class BowlingPlaceBetAction
    : IGameAction<BowlingPlaceBetCommand, BowlingBetState, BowlingBetResult>
{
    public const string DailyRollQuota = "bowling.daily-roll";

    public GameDecision<BowlingBetState, BowlingBetResult> Decide(
        GameActionInput<BowlingBetState, BowlingPlaceBetCommand> input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var command = input.Command;
        var balance = checked((int)input.Wallet.Balance);
        if (command.Amount <= 0 || command.Amount > command.MaxBet)
            return Reject(input.State, BowlingBetResult.Fail(BowlingBetError.InvalidAmount), "invalid_amount");
        if (command.BlockingGameId is not null)
        {
            return Reject(input.State, new BowlingBetResult(
                BowlingBetError.BusyOtherGame, Balance: balance, BlockingGameId: command.BlockingGameId), "busy_other_game");
        }
        if (input.State.PendingBet is { } pending)
            return Reject(input.State, BowlingBetResult.Fail(BowlingBetError.AlreadyPending, balance, pending.Amount), "already_pending");
        if (!input.Quotas.TryGetValue(DailyRollQuota, out var quota))
            throw new InvalidOperationException($"Required quota '{DailyRollQuota}' was not supplied.");
        if (quota.Limit > 0 && quota.Used >= quota.Limit)
        {
            return Reject(input.State, new BowlingBetResult(
                BowlingBetError.DailyRollLimit,
                Balance: balance,
                DailyRollUsed: checked((int)quota.Used),
                DailyRollLimit: checked((int)quota.Limit)), "daily_roll_limit");
        }
        if (command.Amount > input.Wallet.Balance)
            return Reject(input.State, BowlingBetResult.Fail(BowlingBetError.NotEnoughCoins, balance), "insufficient_balance");

        return new GameDecision<BowlingBetState, BowlingBetResult>(
            DecisionStatus.Accepted,
            new BowlingBetState(new BowlingPendingBet(command.UserId, command.ChatId, command.Amount, input.UtcNow)),
            new BowlingBetResult(BowlingBetError.None, command.Amount, checked(balance - command.Amount)),
            [EconomyEffect.Debit(command.Amount, "bowling.bet")],
            quota.Limit > 0 ? [QuotaEffect.Consume(DailyRollQuota)] : [],
            [],
            [new BowlingBetPlaced(command.UserId, command.ChatId, command.Amount, input.UtcNow.ToUnixTimeMilliseconds())],
            []);
    }

    private static GameDecision<BowlingBetState, BowlingBetResult> Reject(
        BowlingBetState state, BowlingBetResult result, string reason) =>
        new(DecisionStatus.Rejected, state, result, [], [], [], [], [], reason);
}
