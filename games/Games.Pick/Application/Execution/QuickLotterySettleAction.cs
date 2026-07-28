using System.Security.Cryptography;
using System.Text;
using BotFramework.Sdk.Execution;
using Games.Pick.Domain.Events;

namespace Games.Pick.Application.Execution;

public sealed class QuickLotterySettleAction : IGameAction<QuickLotterySettleCommand, QuickLotteryState, LotterySettleResult>
{
    public const string WinnerEntropy = "winner";
    public GameDecision<QuickLotteryState, LotterySettleResult> Decide(GameActionInput<QuickLotteryState, QuickLotterySettleCommand> input)
    {
        var c = input.Command;
        var row = input.State.Row ?? c.Row;
        var entries = input.State.Entries;
        var pot = entries.Sum(x => x.StakePaid);
        if (c.ForceCancel || entries.Count < Math.Max(2, c.MinEntrants))
        {
            var cancelled = row with { Status = "cancelled", SettledAt = input.UtcNow.UtcDateTime };
            var effects = entries.Select(x => (IGameEffect)new PickWalletCreditEffect(x.UserId, row.ChatId, x.StakePaid,
                c.ForceCancel ? "pick.lottery.cancel.refund" : "pick.lottery.refund")).ToArray();
            return new(DecisionStatus.Accepted, new(cancelled, entries), new(LotterySettleKind.Cancelled, row, entries, null, null, pot, 0, 0), [], [], [],
                [new PickLotteryCompleted(row.Id, row.ChatId, true, null, entries.Count, pot, 0, 0, input.UtcNow.ToUnixTimeMilliseconds())],
                [], CustomEffects: effects);
        }
        var winner = entries[Math.Min(entries.Count - 1, (int)(input.Entropy.GetDouble(WinnerEntropy) * entries.Count))];
        var fee = (int)Math.Floor(pot * Math.Clamp(c.HouseFee, 0, 1));
        var payout = pot - fee;
        var settled = row with { Status = "settled", SettledAt = input.UtcNow.UtcDateTime, WinnerId = winner.UserId, WinnerName = winner.DisplayName, PotTotal = pot, Payout = payout, Fee = fee };
        return new(DecisionStatus.Accepted, new(settled, entries), new(LotterySettleKind.Settled, row, entries, winner.UserId, winner.DisplayName, pot, fee, payout), [], [], [],
            [new PickLotteryCompleted(row.Id, row.ChatId, false, winner.UserId, entries.Count, pot, payout, fee, input.UtcNow.ToUnixTimeMilliseconds())], [],
            CustomEffects: payout > 0 ? [new PickWalletCreditEffect(winner.UserId, row.ChatId, payout, "pick.lottery.win")] : []);
    }
}
