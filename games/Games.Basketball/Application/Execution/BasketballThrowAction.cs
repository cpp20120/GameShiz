using BotFramework.Sdk.Execution;

namespace Games.Basketball.Application.Execution;

public sealed class BasketballThrowAction
    : IGameAction<BasketballThrowCommand, BasketballBetState, BasketballThrowResult>
{
    public const string RedeemDropEntropy = "redeem-drop";

    public GameDecision<BasketballBetState, BasketballThrowResult> Decide(
        GameActionInput<BasketballBetState, BasketballThrowCommand> input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.State.PendingBet is not { } bet)
        {
            return new GameDecision<BasketballBetState, BasketballThrowResult>(
                DecisionStatus.Rejected,
                input.State,
                new BasketballThrowResult(BasketballThrowOutcome.NoBet),
                [], [], [], [], [], "no_pending_bet");
        }
        if (!input.Quotas.TryGetValue(BasketballPlaceBetAction.DailyRollQuota, out var quota))
            throw new InvalidOperationException($"Required quota '{BasketballPlaceBetAction.DailyRollQuota}' was not supplied.");

        var multiplier = BasketballRules.Multiplier(input.Command.Face);
        var payout = checked(bet.Amount * multiplier);
        var economy = payout > 0
            ? new[] { EconomyEffect.Credit(payout, "basketball.payout") }
            : [];
        var occurredAt = input.UtcNow.ToUnixTimeMilliseconds();
        var events = new List<IDomainEvent>
        {
            new BasketballThrowCompleted(
                input.Command.UserId,
                input.Command.ChatId,
                input.Command.Face,
                bet.Amount,
                multiplier,
                payout,
                occurredAt),
            new GameCompletedMetaEvent(
                input.Command.ChatId,
                input.Command.UserId,
                input.Command.DisplayName,
                MiniGameIds.Basketball,
                bet.Amount,
                payout,
                payout > bet.Amount,
                decimal.Divide(payout, bet.Amount),
                occurredAt),
        };
        if (input.Command.RedeemDropChance > 0
            && input.Entropy.GetDouble(RedeemDropEntropy) < input.Command.RedeemDropChance)
        {
            events.Add(new TelegramMiniGameRedeemCodeDropRequested(
                input.Command.UserId,
                input.Command.ChatId,
                MiniGameIds.Basketball,
                occurredAt));
        }

        return new GameDecision<BasketballBetState, BasketballThrowResult>(
            DecisionStatus.Accepted,
            new BasketballBetState(null),
            new BasketballThrowResult(
                BasketballThrowOutcome.Thrown,
                input.Command.Face,
                bet.Amount,
                multiplier,
                payout,
                checked((int)input.Wallet.Balance + payout),
                quota.Limit > 0 ? checked((int)quota.Used) : 0,
                checked((int)quota.Limit)),
            economy,
            [],
            [],
            events,
            []);
    }
}
