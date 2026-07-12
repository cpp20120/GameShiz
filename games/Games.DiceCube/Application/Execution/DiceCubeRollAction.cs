using BotFramework.Sdk.Execution;

namespace Games.DiceCube.Application.Execution;

public sealed class DiceCubeRollAction
    : IGameAction<DiceCubeRollCommand, DiceCubePlaceBetState, CubeRollResult>
{
    public const string RedeemDropEntropy = "redeem-drop";

    public GameDecision<DiceCubePlaceBetState, CubeRollResult> Decide(
        GameActionInput<DiceCubePlaceBetState, DiceCubeRollCommand> input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.State.PendingBet is not { } bet)
        {
            return new GameDecision<DiceCubePlaceBetState, CubeRollResult>(
                DecisionStatus.Rejected,
                input.State,
                new CubeRollResult(CubeRollOutcome.NoBet),
                [], [], [], [], [], "no_pending_bet");
        }

        if (!input.Quotas.TryGetValue(DiceCubePlaceBetAction.DailyRollQuota, out var quota))
            throw new InvalidOperationException($"Required quota '{DiceCubePlaceBetAction.DailyRollQuota}' was not supplied.");

        var multiplier = input.Command.Face switch
        {
            4 => bet.Mult4,
            5 => bet.Mult5,
            6 => bet.Mult6,
            _ => 0,
        };
        var payout = checked(bet.Amount * multiplier);
        var economy = payout > 0
            ? new[] { EconomyEffect.Credit(payout, "dicecube.payout") }
            : [];
        var occurredAt = input.UtcNow.ToUnixTimeMilliseconds();
        var events = new List<IDomainEvent>
        {
            new DiceCubeRollCompleted(
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
                MiniGameIds.DiceCube,
                bet.Amount,
                payout,
                payout > bet.Amount,
                bet.Amount > 0 ? decimal.Divide(payout, bet.Amount) : 0m,
                occurredAt),
        };
        if (input.Command.RedeemDropChance > 0
            && input.Entropy.GetDouble(RedeemDropEntropy) < input.Command.RedeemDropChance)
        {
            events.Add(new TelegramMiniGameRedeemCodeDropRequested(
                input.Command.UserId,
                input.Command.ChatId,
                MiniGameIds.DiceCube,
                occurredAt));
        }

        return new GameDecision<DiceCubePlaceBetState, CubeRollResult>(
            DecisionStatus.Accepted,
            new DiceCubePlaceBetState(null),
            new CubeRollResult(
                CubeRollOutcome.Rolled,
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
