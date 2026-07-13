using BotFramework.Sdk.Execution;

namespace Games.Basketball.Application.Execution;

public sealed class BasketballPlaceBetAction
    : IGameAction<BasketballPlaceBetCommand, BasketballBetState, BasketballBetResult>
{
    public const string DailyRollQuota = "basketball.daily-roll";

    public GameDecision<BasketballBetState, BasketballBetResult> Decide(
        GameActionInput<BasketballBetState, BasketballPlaceBetCommand> input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var command = input.Command;
        var balance = checked((int)input.Wallet.Balance);

        if (command.Amount <= 0 || command.Amount > command.MaxBet)
            return Reject(input.State, BasketballBetResult.Fail(BasketballBetError.InvalidAmount), "invalid_amount");
        if (command.BlockingGameId is not null)
        {
            return Reject(
                input.State,
                new BasketballBetResult(
                    BasketballBetError.BusyOtherGame,
                    Balance: balance,
                    BlockingGameId: command.BlockingGameId),
                "busy_other_game");
        }
        if (input.State.PendingBet is { } pending)
        {
            return Reject(
                input.State,
                BasketballBetResult.Fail(BasketballBetError.AlreadyPending, balance, pending.Amount),
                "already_pending");
        }
        if (!input.Quotas.TryGetValue(DailyRollQuota, out var quota))
            throw new InvalidOperationException($"Required quota '{DailyRollQuota}' was not supplied.");
        if (quota.Limit > 0 && quota.Used >= quota.Limit)
        {
            return Reject(
                input.State,
                new BasketballBetResult(
                    BasketballBetError.DailyRollLimit,
                    Balance: balance,
                    DailyRollUsed: checked((int)quota.Used),
                    DailyRollLimit: checked((int)quota.Limit)),
                "daily_roll_limit");
        }
        if (command.Amount > input.Wallet.Balance)
            return Reject(input.State, BasketballBetResult.Fail(BasketballBetError.NotEnoughCoins, balance), "insufficient_balance");

        var pendingBet = new BasketballPendingBet(
            command.UserId,
            command.ChatId,
            command.Amount,
            input.UtcNow);
        return new GameDecision<BasketballBetState, BasketballBetResult>(
            DecisionStatus.Accepted,
            new BasketballBetState(pendingBet),
            new BasketballBetResult(BasketballBetError.None, command.Amount, checked(balance - command.Amount)),
            [EconomyEffect.Debit(command.Amount, "basketball.bet")],
            quota.Limit > 0 ? [QuotaEffect.Consume(DailyRollQuota)] : [],
            [],
            [new BasketballBetPlaced(
                command.UserId,
                command.ChatId,
                command.Amount,
                input.UtcNow.ToUnixTimeMilliseconds())],
            []);
    }

    private static GameDecision<BasketballBetState, BasketballBetResult> Reject(
        BasketballBetState state,
        BasketballBetResult result,
        string reason) =>
        new(DecisionStatus.Rejected, state, result, [], [], [], [], [], reason);
}
