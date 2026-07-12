using BotFramework.Sdk.Execution;

namespace Games.DiceCube.Application.Execution;

public sealed class DiceCubePlaceBetAction
    : IGameAction<DiceCubePlaceBetCommand, DiceCubePlaceBetState, CubeBetResult>
{
    public const string DailyRollQuota = "dicecube.daily-roll";

    public GameDecision<DiceCubePlaceBetState, CubeBetResult> Decide(
        GameActionInput<DiceCubePlaceBetState, DiceCubePlaceBetCommand> input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var command = input.Command;
        var balance = checked((int)input.Wallet.Balance);

        if (command.Amount <= 0 || command.Amount > command.MaxBet)
            return Reject(input.State, CubeBetResult.Fail(CubeBetError.InvalidAmount), "invalid_amount");
        if (command.CooldownSeconds > 0)
            return Reject(input.State, CubeBetResult.CooldownWait(balance, command.CooldownSeconds), "cooldown");
        if (command.BlockingGameId is not null)
        {
            return Reject(
                input.State,
                new CubeBetResult(CubeBetError.BusyOtherGame, Balance: balance, BlockingGameId: command.BlockingGameId),
                "busy_other_game");
        }
        if (input.State.PendingBet is { } pending)
        {
            return Reject(
                input.State,
                CubeBetResult.Fail(CubeBetError.AlreadyPending, balance, pending.Amount),
                "already_pending");
        }
        if (!input.Quotas.TryGetValue(DailyRollQuota, out var quota))
            throw new InvalidOperationException($"Required quota '{DailyRollQuota}' was not supplied.");
        if (quota.Limit > 0 && quota.Used >= quota.Limit)
        {
            return Reject(
                input.State,
                new CubeBetResult(
                    CubeBetError.DailyRollLimit,
                    Balance: balance,
                    DailyRollUsed: checked((int)quota.Used),
                    DailyRollLimit: checked((int)quota.Limit)),
                "daily_roll_limit");
        }
        if (command.Amount > input.Wallet.Balance)
            return Reject(input.State, CubeBetResult.Fail(CubeBetError.NotEnoughCoins, balance), "insufficient_balance");

        var pendingBet = new DiceCubePendingBet(
            command.UserId,
            command.ChatId,
            command.Amount,
            input.UtcNow,
            command.Mult4,
            command.Mult5,
            command.Mult6);
        var newBalance = checked(balance - command.Amount);
        return new GameDecision<DiceCubePlaceBetState, CubeBetResult>(
            DecisionStatus.Accepted,
            new DiceCubePlaceBetState(pendingBet),
            new CubeBetResult(CubeBetError.None, command.Amount, newBalance),
            [EconomyEffect.Debit(command.Amount, "dicecube.bet")],
            quota.Limit > 0 ? [QuotaEffect.Consume(DailyRollQuota)] : [],
            [],
            [new DiceCubeBetPlaced(
                command.UserId,
                command.ChatId,
                command.Amount,
                input.UtcNow.ToUnixTimeMilliseconds())],
            []);
    }

    private static GameDecision<DiceCubePlaceBetState, CubeBetResult> Reject(
        DiceCubePlaceBetState state,
        CubeBetResult result,
        string reason) =>
        new(DecisionStatus.Rejected, state, result, [], [], [], [], [], reason);
}
