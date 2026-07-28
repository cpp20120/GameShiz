using System.Security.Cryptography;
using System.Text;
using BotFramework.Sdk.Execution;
using Games.Pick.Domain.Events;

namespace Games.Pick.Application.Execution;

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
