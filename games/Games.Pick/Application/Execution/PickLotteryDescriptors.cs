using BotFramework.Host.Execution;

namespace Games.Pick.Application.Execution;

public abstract class QuickDescriptor<TCommand,TResult> : GameExecutionDescriptor<TCommand,QuickLotteryState,TResult>
{
    public override string GameId => "pick-lottery";
}
public sealed class QuickOpenDescriptor : QuickDescriptor<QuickLotteryOpenCommand,LotteryOpenResult>
{ public override string CommandId(QuickLotteryOpenCommand c)=>c.CommandId; public override string AggregateId(QuickLotteryOpenCommand c)=>c.ChatId.ToString(); public override long ChatId(QuickLotteryOpenCommand c)=>c.ChatId; public override string DisplayName(QuickLotteryOpenCommand c)=>c.DisplayName; public override WalletIdentity Wallet(QuickLotteryOpenCommand c)=>new(c.UserId,c.ChatId); }
public sealed class QuickJoinDescriptor : QuickDescriptor<QuickLotteryJoinCommand,LotteryJoinResult>
{ public override string CommandId(QuickLotteryJoinCommand c)=>c.CommandId; public override string AggregateId(QuickLotteryJoinCommand c)=>c.ChatId.ToString(); public override long ChatId(QuickLotteryJoinCommand c)=>c.ChatId; public override string DisplayName(QuickLotteryJoinCommand c)=>c.DisplayName; public override WalletIdentity Wallet(QuickLotteryJoinCommand c)=>new(c.UserId,c.ChatId); }
public sealed class QuickSettleDescriptor : QuickDescriptor<QuickLotterySettleCommand,LotterySettleResult>
{ public override bool UsesPrimaryWallet=>false; public override IReadOnlyList<string> EntropyNames=>[QuickLotterySettleAction.WinnerEntropy]; public override string CommandId(QuickLotterySettleCommand c)=>c.CommandId; public override string AggregateId(QuickLotterySettleCommand c)=>c.Row.ChatId.ToString(); public override long ChatId(QuickLotterySettleCommand c)=>c.Row.ChatId; public override string DisplayName(QuickLotterySettleCommand c)=>"lottery"; public override WalletIdentity Wallet(QuickLotterySettleCommand c)=>new(0,c.Row.ChatId); public override IReadOnlyList<string> AdditionalLockKeys(QuickLotterySettleCommand c)=>c.ExpectedEntries.Select(x=>new WalletIdentity(x.UserId,c.Row.ChatId).LockKey).Distinct().ToArray(); }
