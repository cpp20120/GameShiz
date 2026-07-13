using BotFramework.Sdk.Execution;

namespace Games.Basketball.Application.Execution;

public sealed class BasketballAbortAction
    : IGameAction<BasketballAbortCommand, BasketballBetState, BasketballAbortResult>
{
    public GameDecision<BasketballBetState, BasketballAbortResult> Decide(
        GameActionInput<BasketballBetState, BasketballAbortCommand> input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.State.PendingBet is not { } bet)
        {
            return new GameDecision<BasketballBetState, BasketballAbortResult>(
                DecisionStatus.Rejected,
                input.State,
                new BasketballAbortResult(false),
                [], [], [], [], [], "no_pending_bet");
        }
        if (!input.Quotas.TryGetValue(BasketballPlaceBetAction.DailyRollQuota, out var quota))
            throw new InvalidOperationException($"Required quota '{BasketballPlaceBetAction.DailyRollQuota}' was not supplied.");

        return new GameDecision<BasketballBetState, BasketballAbortResult>(
            DecisionStatus.Accepted,
            new BasketballBetState(null),
            new BasketballAbortResult(true),
            [EconomyEffect.Credit(bet.Amount, "basketball.send_dice_failed")],
            quota.Limit > 0 ? [QuotaEffect.Restore(BasketballPlaceBetAction.DailyRollQuota)] : [],
            [],
            [new BasketballBetAborted(
                input.Command.UserId,
                input.Command.ChatId,
                bet.Amount,
                input.UtcNow.ToUnixTimeMilliseconds())],
            []);
    }
}
