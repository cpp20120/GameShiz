using System.Security.Cryptography;
using System.Text;
using BotFramework.Sdk.Execution;
using Games.Pick.Domain.Events;

namespace Games.Pick.Application.Execution;

public sealed record QuickLotteryState(PickLotteryRow? Row, IReadOnlyList<PickLotteryEntryRow> Entries);
public sealed record QuickLotteryOpenCommand(long UserId, string DisplayName, long ChatId, int Stake, string CommandId, int MinStake, int MaxStake, int DurationSeconds);
public sealed record QuickLotteryJoinCommand(long UserId, string DisplayName, long ChatId, string CommandId);
public sealed record QuickLotterySettleCommand(PickLotteryRow Row, IReadOnlyList<PickLotteryEntryRow> ExpectedEntries, bool ForceCancel, string CommandId, int MinEntrants, double HouseFee);
public sealed record PickWalletCreditEffect(long UserId, long ChatId, int Amount, string Reason) : IGameEffect;

public sealed class QuickLotteryOpenAction : IGameAction<QuickLotteryOpenCommand, QuickLotteryState, LotteryOpenResult>
{
    public GameDecision<QuickLotteryState, LotteryOpenResult> Decide(GameActionInput<QuickLotteryState, QuickLotteryOpenCommand> input)
    {
        var c = input.Command;
        if (c.Stake < Math.Max(1, c.MinStake) || c.MaxStake > 0 && c.Stake > c.MaxStake)
            return Reject(input.State, new(LotteryOpenStatus.InvalidStake, null, 0), "invalid_stake");
        if (input.State.Row is { } existing)
            return Reject(input.State, new(LotteryOpenStatus.AlreadyOpen, existing, checked((int)input.Wallet.Balance)), "already_open");
        if (c.Stake > input.Wallet.Balance)
            return Reject(input.State, new(LotteryOpenStatus.NotEnoughCoins, null, checked((int)input.Wallet.Balance)), "insufficient_balance");
        var row = new PickLotteryRow(Id(c.CommandId), c.ChatId, c.UserId, c.DisplayName, c.Stake, "open",
            input.UtcNow.UtcDateTime, input.UtcNow.AddSeconds(Math.Max(10, c.DurationSeconds)).UtcDateTime,
            null, null, null, null, null, null);
        var entry = new PickLotteryEntryRow(row.Id, c.UserId, c.DisplayName, c.Stake, input.UtcNow.UtcDateTime);
        return new(DecisionStatus.Accepted, new(row, [entry]),
            new(LotteryOpenStatus.Ok, row, checked((int)input.Wallet.Balance - c.Stake)),
            [EconomyEffect.Debit(c.Stake, "pick.lottery.open")], [], [],
            [new PickLotteryOpened(row.Id, c.UserId, c.ChatId, c.Stake, input.UtcNow.ToUnixTimeMilliseconds())],
            []);
    }
    private static Guid Id(string value) => new(SHA256.HashData(Encoding.UTF8.GetBytes(value)).AsSpan(0, 16));
    private static GameDecision<QuickLotteryState, LotteryOpenResult> Reject(QuickLotteryState s, LotteryOpenResult r, string reason) => new(DecisionStatus.Rejected, s, r, [], [], [], [], [], reason);
}

public sealed class QuickLotteryJoinAction : IGameAction<QuickLotteryJoinCommand, QuickLotteryState, LotteryJoinResult>
{
    public GameDecision<QuickLotteryState, LotteryJoinResult> Decide(GameActionInput<QuickLotteryState, QuickLotteryJoinCommand> input)
    {
        var c = input.Command;
        if (input.State.Row is not { } row)
            return Reject(input.State, new(LotteryJoinStatus.NoOpenLottery, null, 0, 0, 0), "no_open");
        if (input.State.Entries.Any(x => x.UserId == c.UserId))
            return Reject(input.State, Result(LotteryJoinStatus.AlreadyJoined, row, input.State.Entries, (int)input.Wallet.Balance), "already_joined");
        if (row.Stake > input.Wallet.Balance)
            return Reject(input.State, new(LotteryJoinStatus.NotEnoughCoins, row, 0, 0, (int)input.Wallet.Balance), "insufficient_balance");
        var entries = input.State.Entries.Append(new PickLotteryEntryRow(row.Id, c.UserId, c.DisplayName, row.Stake, input.UtcNow.UtcDateTime)).ToArray();
        return new(DecisionStatus.Accepted, new(row, entries), Result(LotteryJoinStatus.Ok, row, entries, (int)input.Wallet.Balance - row.Stake),
            [EconomyEffect.Debit(row.Stake, "pick.lottery.join")], [], [],
            [new PickLotteryJoined(row.Id, c.UserId, c.ChatId, row.Stake, entries.Length, input.UtcNow.ToUnixTimeMilliseconds())],
            []);
    }
    private static LotteryJoinResult Result(LotteryJoinStatus status, PickLotteryRow row, IReadOnlyList<PickLotteryEntryRow> entries, int balance) => new(status, row, entries.Count, entries.Sum(x => x.StakePaid), balance);
    private static GameDecision<QuickLotteryState, LotteryJoinResult> Reject(QuickLotteryState s, LotteryJoinResult r, string reason) => new(DecisionStatus.Rejected, s, r, [], [], [], [], [], reason);
}

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
