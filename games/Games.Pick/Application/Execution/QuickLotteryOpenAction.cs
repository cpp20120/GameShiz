using System.Security.Cryptography;
using System.Text;
using BotFramework.Sdk.Execution;
using Games.Pick.Domain.Events;

namespace Games.Pick.Application.Execution;

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
