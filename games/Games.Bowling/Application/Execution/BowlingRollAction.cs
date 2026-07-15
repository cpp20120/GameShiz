using BotFramework.Contracts.Messaging;
using BotFramework.Sdk.Execution;

namespace Games.Bowling.Application.Execution;

public sealed class BowlingRollAction : IGameAction<BowlingRollCommand, BowlingBetState, BowlingRollResult>
{
    public const string RedeemDropEntropy = "redeem-drop";

    public GameDecision<BowlingBetState, BowlingRollResult> Decide(
        GameActionInput<BowlingBetState, BowlingRollCommand> input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.State.PendingBet is not { } bet)
            return new(DecisionStatus.Rejected, input.State, new(BowlingRollOutcome.NoBet), [], [], [], [], [], "no_pending_bet");
        if (!input.Quotas.TryGetValue(BowlingPlaceBetAction.DailyRollQuota, out var quota))
            throw new InvalidOperationException($"Required quota '{BowlingPlaceBetAction.DailyRollQuota}' was not supplied.");

        var multiplier = BowlingRules.Multiplier(input.Command.Face);
        var payout = checked(bet.Amount * multiplier);
        var occurredAt = input.UtcNow.ToUnixTimeMilliseconds();
        var events = new List<IDomainEvent>
        {
            new BowlingRollCompleted(input.Command.UserId, input.Command.ChatId, input.Command.Face, bet.Amount, multiplier, payout, occurredAt),
            new GameCompletedMetaEvent(
                input.Command.ChatId, input.Command.UserId, input.Command.DisplayName, MiniGameIds.Bowling,
                bet.Amount, payout, payout > bet.Amount, decimal.Divide(payout, bet.Amount), occurredAt),
        };
        if (input.Command.RedeemDropChance > 0
            && input.Entropy.GetDouble(RedeemDropEntropy) < input.Command.RedeemDropChance)
        {
            events.Add(new MiniGameRedeemCodeDropRequested(
                input.Command.UserId, input.Command.ChatId, MiniGameIds.Bowling, occurredAt,
                BotChannelContext.Current));
        }

        return new GameDecision<BowlingBetState, BowlingRollResult>(
            DecisionStatus.Accepted,
            new BowlingBetState(null),
            new BowlingRollResult(
                BowlingRollOutcome.Rolled, input.Command.Face, bet.Amount, multiplier, payout,
                checked((int)input.Wallet.Balance + payout),
                quota.Limit > 0 ? checked((int)quota.Used) : 0,
                checked((int)quota.Limit)),
            payout > 0 ? [EconomyEffect.Credit(payout, "bowling.payout")] : [],
            [], [], events, []);
    }
}
