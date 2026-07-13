using BotFramework.Sdk.Execution;

namespace Games.Bowling.Application.Execution;

public sealed class BowlingAbortAction : IGameAction<BowlingAbortCommand, BowlingBetState, BowlingAbortResult>
{
    public GameDecision<BowlingBetState, BowlingAbortResult> Decide(
        GameActionInput<BowlingBetState, BowlingAbortCommand> input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.State.PendingBet is not { } bet)
            return new(DecisionStatus.Rejected, input.State, new(false), [], [], [], [], [], "no_pending_bet");
        if (!input.Quotas.TryGetValue(BowlingPlaceBetAction.DailyRollQuota, out var quota))
            throw new InvalidOperationException($"Required quota '{BowlingPlaceBetAction.DailyRollQuota}' was not supplied.");
        return new(
            DecisionStatus.Accepted,
            new BowlingBetState(null),
            new BowlingAbortResult(true),
            [EconomyEffect.Credit(bet.Amount, "bowling.send_dice_failed")],
            quota.Limit > 0 ? [QuotaEffect.Restore(BowlingPlaceBetAction.DailyRollQuota)] : [],
            [],
            [new BowlingBetAborted(input.Command.UserId, input.Command.ChatId, bet.Amount, input.UtcNow.ToUnixTimeMilliseconds())],
            []);
    }
}
