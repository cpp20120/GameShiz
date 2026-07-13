using BotFramework.Sdk.Execution;

namespace Games.Football.Application.Execution;

public sealed class FootballPlaceBetAction
    : IGameAction<FootballPlaceBetCommand, FootballBetState, FootballBetResult>
{
    public const string DailyRollQuota = "football.daily-roll";

    public GameDecision<FootballBetState, FootballBetResult> Decide(
        GameActionInput<FootballBetState, FootballPlaceBetCommand> input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var command = input.Command;
        var balance = checked((int)input.Wallet.Balance);
        if (command.Amount <= 0 || command.Amount > command.MaxBet)
            return Reject(input.State, FootballBetResult.Fail(FootballBetError.InvalidAmount), "invalid_amount");
        if (command.BlockingGameId is not null)
            return Reject(input.State, new(FootballBetError.BusyOtherGame, Balance: balance, BlockingGameId: command.BlockingGameId), "busy_other_game");
        if (input.State.PendingBet is { } pending)
            return Reject(input.State, FootballBetResult.Fail(FootballBetError.AlreadyPending, balance, pending.Amount), "already_pending");
        if (!input.Quotas.TryGetValue(DailyRollQuota, out var quota))
            throw new InvalidOperationException($"Required quota '{DailyRollQuota}' was not supplied.");
        if (quota.Limit > 0 && quota.Used >= quota.Limit)
        {
            return Reject(input.State, new(
                FootballBetError.DailyRollLimit,
                Balance: balance,
                DailyRollUsed: checked((int)quota.Used),
                DailyRollLimit: checked((int)quota.Limit)), "daily_roll_limit");
        }
        if (command.Amount > input.Wallet.Balance)
            return Reject(input.State, FootballBetResult.Fail(FootballBetError.NotEnoughCoins, balance), "insufficient_balance");

        return new(
            DecisionStatus.Accepted,
            new FootballBetState(new(command.UserId, command.ChatId, command.Amount, input.UtcNow)),
            new FootballBetResult(FootballBetError.None, command.Amount, checked(balance - command.Amount)),
            [EconomyEffect.Debit(command.Amount, "football.bet")],
            quota.Limit > 0 ? [QuotaEffect.Consume(DailyRollQuota)] : [],
            [],
            [new FootballBetPlaced(command.UserId, command.ChatId, command.Amount, input.UtcNow.ToUnixTimeMilliseconds())],
            []);
    }

    private static GameDecision<FootballBetState, FootballBetResult> Reject(
        FootballBetState state, FootballBetResult result, string reason) =>
        new(DecisionStatus.Rejected, state, result, [], [], [], [], [], reason);
}
