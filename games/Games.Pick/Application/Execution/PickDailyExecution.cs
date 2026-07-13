using BotFramework.Sdk.Execution;
using Games.Pick.Domain.Events;

namespace Games.Pick.Application.Execution;

public sealed record DailyTicketOwner(long UserId,string DisplayName);
public sealed record DailyLotteryState(PickDailyLotteryRow Row,IReadOnlyList<DailyTicketOwner> Tickets);
public sealed record DailyBuyCommand(long UserId,string DisplayName,long ChatId,int Count,string CommandId,DateOnly DayLocal,DateTime DeadlineUtc,int TicketPrice,int PerCommandCap,int UserCap);
public sealed record DailySettleCommand(PickDailyLotteryRow Row,IReadOnlyList<DailyTicketOwner> ExpectedTickets,string CommandId,double HouseFee);

public sealed class DailyBuyAction : IGameAction<DailyBuyCommand,DailyLotteryState,DailyBuyResult>
{
    public GameDecision<DailyLotteryState,DailyBuyResult> Decide(GameActionInput<DailyLotteryState,DailyBuyCommand> input)
    {
        var c=input.Command;var row=input.State.Row;var owned=input.State.Tickets.Count(x=>x.UserId==c.UserId);
        if(c.Count<=0)return Reject(input.State,new(DailyBuyStatus.InvalidCount,null,0,0,0,0,0),"invalid_count");
        if(c.Count>Math.Max(1,c.PerCommandCap))return Reject(input.State,new(DailyBuyStatus.OverPerCommandCap,null,0,0,0,0,0),"command_cap");
        if(row.Status!="open"||row.DeadlineAt<=input.UtcNow.UtcDateTime)return Reject(input.State,new(DailyBuyStatus.DayAlreadyClosing,row,0,0,0,0,0),"closing");
        if(c.UserCap>0&&owned+c.Count>c.UserCap)return Reject(input.State,new(DailyBuyStatus.OverDailyCap,row,0,owned,0,0,0),"daily_cap");
        var cost=checked(c.Count*row.TicketPrice);if(cost>input.Wallet.Balance)return Reject(input.State,new(DailyBuyStatus.NotEnoughCoins,row,0,owned,0,0,(int)input.Wallet.Balance),"insufficient_balance");
        var tickets=input.State.Tickets.Concat(Enumerable.Repeat(new DailyTicketOwner(c.UserId,c.DisplayName),c.Count)).ToArray();
        return new(DecisionStatus.Accepted,new(row,tickets),new(DailyBuyStatus.Ok,row,c.Count,owned+c.Count,tickets.Length,tickets.Length*row.TicketPrice,(int)input.Wallet.Balance-cost),[EconomyEffect.Debit(cost,"pick.daily.buy")],[],[],[new PickDailyTicketsBought(row.Id,c.UserId,c.ChatId,c.Count,cost,input.UtcNow.ToUnixTimeMilliseconds())],[]);
    }
    private static GameDecision<DailyLotteryState,DailyBuyResult> Reject(DailyLotteryState s,DailyBuyResult r,string reason)=>new(DecisionStatus.Rejected,s,r,[],[],[],[],[],reason);
}

public sealed class DailySettleAction : IGameAction<DailySettleCommand,DailyLotteryState,DailySettleResult>
{
    public const string WinnerEntropy="winner";
    public GameDecision<DailyLotteryState,DailySettleResult> Decide(GameActionInput<DailyLotteryState,DailySettleCommand> input)
    {
        var row=input.State.Row;var tickets=input.State.Tickets;var distinct=tickets.Select(x=>x.UserId).Distinct().Count();var pot=tickets.Count*row.TicketPrice;
        if(tickets.Count==0){var cancelled=row with{Status="cancelled",SettledAt=input.UtcNow.UtcDateTime};return new(DecisionStatus.Accepted,new(cancelled,tickets),new(false,row,0,0,0,0,0,null,null,null),[],[],[],[new PickDailyLotteryCompleted(row.Id,row.ChatId,true,null,0,0,0,0,input.UtcNow.ToUnixTimeMilliseconds())],[]);}
        var winner=tickets[Math.Min(tickets.Count-1,(int)(input.Entropy.GetDouble(WinnerEntropy)*tickets.Count))];var fee=(int)Math.Floor(pot*Math.Clamp(input.Command.HouseFee,0,1));var payout=pot-fee;var count=tickets.Count(x=>x.UserId==winner.UserId);
        var settled=row with{Status="settled",SettledAt=input.UtcNow.UtcDateTime,WinnerId=winner.UserId,WinnerName=winner.DisplayName,TicketCount=tickets.Count,PotTotal=pot,Payout=payout,Fee=fee};
        return new(DecisionStatus.Accepted,new(settled,tickets),new(true,row,tickets.Count,distinct,pot,fee,payout,winner.UserId,winner.DisplayName,count),[],[],[],[new PickDailyLotteryCompleted(row.Id,row.ChatId,false,winner.UserId,tickets.Count,pot,payout,fee,input.UtcNow.ToUnixTimeMilliseconds())],[],CustomEffects:payout>0?[new PickWalletCreditEffect(winner.UserId,row.ChatId,payout,"pick.daily.win")]:[]);
    }
}
