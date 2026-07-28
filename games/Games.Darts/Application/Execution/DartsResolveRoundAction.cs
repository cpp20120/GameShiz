using BotFramework.Contracts.Messaging;
using BotFramework.Sdk.Execution;

namespace Games.Darts.Application.Execution;

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
            events.Add(new MiniGameRedeemCodeDropRequested(
                command.UserId, command.ChatId, MiniGameIds.Darts, occurredAt,
                input.State.Round?.Channel ?? BotChannelContext.Current));
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
