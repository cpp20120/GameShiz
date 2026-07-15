using BotFramework.Contracts.Messaging;
using BotFramework.Sdk.Execution;

namespace Games.Football.Application.Execution;

public sealed class FootballThrowAction : IGameAction<FootballThrowCommand, FootballBetState, FootballThrowResult>
{
    public const string RedeemDropEntropy = "redeem-drop";

    public GameDecision<FootballBetState, FootballThrowResult> Decide(
        GameActionInput<FootballBetState, FootballThrowCommand> input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.State.PendingBet is not { } bet)
            return new(DecisionStatus.Rejected, input.State, new(FootballThrowOutcome.NoBet), [], [], [], [], [], "no_pending_bet");
        if (!input.Quotas.TryGetValue(FootballPlaceBetAction.DailyRollQuota, out var quota))
            throw new InvalidOperationException($"Required quota '{FootballPlaceBetAction.DailyRollQuota}' was not supplied.");

        var multiplier = FootballRules.Multiplier(input.Command.Face);
        var payout = checked(bet.Amount * multiplier);
        var occurredAt = input.UtcNow.ToUnixTimeMilliseconds();
        var events = new List<IDomainEvent>
        {
            new FootballThrowCompleted(input.Command.UserId, input.Command.ChatId, input.Command.Face, bet.Amount, multiplier, payout, occurredAt),
            new GameCompletedMetaEvent(
                input.Command.ChatId, input.Command.UserId, input.Command.DisplayName, MiniGameIds.Football,
                bet.Amount, payout, payout > bet.Amount, decimal.Divide(payout, bet.Amount), occurredAt),
        };
        if (input.Command.RedeemDropChance > 0
            && input.Entropy.GetDouble(RedeemDropEntropy) < input.Command.RedeemDropChance)
        {
            events.Add(new MiniGameRedeemCodeDropRequested(
                input.Command.UserId, input.Command.ChatId, MiniGameIds.Football, occurredAt,
                BotChannelContext.Current));
        }
        return new(
            DecisionStatus.Accepted,
            new FootballBetState(null),
            new FootballThrowResult(
                FootballThrowOutcome.Thrown, input.Command.Face, bet.Amount, multiplier, payout,
                checked((int)input.Wallet.Balance + payout),
                quota.Limit > 0 ? checked((int)quota.Used) : 0,
                checked((int)quota.Limit)),
            payout > 0 ? [EconomyEffect.Credit(payout, "football.payout")] : [],
            [], [], events, []);
    }
}
