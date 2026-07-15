using BotFramework.Contracts.Messaging;
using BotFramework.Sdk.Execution;

namespace Games.Darts.Application.Execution;

public sealed class DartsQuickThrowAction
    : IGameAction<DartsQuickThrowCommand, NoGameState, DartsThrowResult>
{
    public const string DailyRollQuota = "darts.daily-roll";
    public const string RedeemDropEntropy = "redeem-drop";

    public GameDecision<NoGameState, DartsThrowResult> Decide(
        GameActionInput<NoGameState, DartsQuickThrowCommand> input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var command = input.Command;
        if (command.Amount <= 0 || command.Amount > command.MaxBet)
            return Reject(new DartsThrowResult(DartsThrowOutcome.BetInvalid), "invalid_amount");
        if (command.BlockingGameId is not null)
        {
            return Reject(
                new DartsThrowResult(
                    DartsThrowOutcome.BetBusyOtherGame,
                    BlockingGameId: command.BlockingGameId),
                "busy_other_game");
        }
        if (!input.Quotas.TryGetValue(DailyRollQuota, out var quota))
            throw new InvalidOperationException($"Required quota '{DailyRollQuota}' was not supplied.");
        if (quota.Limit > 0 && quota.Used >= quota.Limit)
        {
            return Reject(
                new DartsThrowResult(
                    DartsThrowOutcome.BetDailyLimit,
                    DailyRollUsed: checked((int)quota.Used),
                    DailyRollLimit: checked((int)quota.Limit)),
                "daily_roll_limit");
        }
        if (command.Amount > input.Wallet.Balance)
        {
            return Reject(
                new DartsThrowResult(
                    DartsThrowOutcome.BetNotEnoughCoins,
                    Balance: checked((int)input.Wallet.Balance)),
                "insufficient_balance");
        }

        var multiplier = command.Face switch { 4 => 1, 5 or 6 => 2, _ => 0 };
        var payout = checked(command.Amount * multiplier);
        var economy = payout > 0
            ? new[]
            {
                EconomyEffect.Debit(command.Amount, "darts.quickplay.bet"),
                EconomyEffect.Credit(payout, "darts.quickplay.payout"),
            }
            : [EconomyEffect.Debit(command.Amount, "darts.quickplay.bet")];
        var occurredAt = input.UtcNow.ToUnixTimeMilliseconds();
        var events = new List<IDomainEvent>
        {
            new DartsThrowCompleted(
                command.UserId, command.ChatId, command.Face, command.Amount, multiplier, payout, occurredAt),
            new GameCompletedMetaEvent(
                command.ChatId,
                command.UserId,
                command.DisplayName,
                MiniGameIds.Darts,
                command.Amount,
                payout,
                payout > command.Amount,
                decimal.Divide(payout, command.Amount),
                occurredAt),
        };
        if (command.RedeemDropChance > 0
            && input.Entropy.GetDouble(RedeemDropEntropy) < command.RedeemDropChance)
        {
            events.Add(new MiniGameRedeemCodeDropRequested(
                command.UserId, command.ChatId, MiniGameIds.Darts, occurredAt,
                BotChannelContext.Current));
        }

        return new GameDecision<NoGameState, DartsThrowResult>(
            DecisionStatus.Accepted,
            input.State,
            new DartsThrowResult(
                DartsThrowOutcome.Thrown,
                command.Face,
                command.Amount,
                multiplier,
                payout,
                checked((int)input.Wallet.Balance - command.Amount + payout),
                DailyRollUsed: quota.Limit > 0 ? checked((int)quota.Used + 1) : 0,
                DailyRollLimit: checked((int)quota.Limit)),
            economy,
            quota.Limit > 0 ? [QuotaEffect.Consume(DailyRollQuota)] : [],
            [],
            events,
            []);
    }

    private static GameDecision<NoGameState, DartsThrowResult> Reject(
        DartsThrowResult result,
        string reason) =>
        new(DecisionStatus.Rejected, default, result, [], [], [], [], [], reason);
}
