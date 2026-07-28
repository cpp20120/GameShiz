using BotFramework.Host.Execution;
namespace Games.Pick.Application.Execution;
public sealed class DailyBuyDescriptor:GameExecutionDescriptor<DailyBuyCommand,DailyLotteryState,DailyBuyResult>
{public override string GameId=>"pick-daily";public override string CommandId(DailyBuyCommand c)=>c.CommandId;public override string AggregateId(DailyBuyCommand c)=>$"{c.ChatId}:{c.DayLocal:yyyy-MM-dd}";public override long ChatId(DailyBuyCommand c)=>c.ChatId;public override string DisplayName(DailyBuyCommand c)=>c.DisplayName;public override WalletIdentity Wallet(DailyBuyCommand c)=>new(c.UserId,c.ChatId);}
