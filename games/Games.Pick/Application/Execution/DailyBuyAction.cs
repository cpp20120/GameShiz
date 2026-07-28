using BotFramework.Sdk.Execution;
using Games.Pick.Domain.Events;

namespace Games.Pick.Application.Execution;

public sealed class DailyBuyAction : IGameAction<DailyBuyCommand,DailyLotteryState,DailyBuyResult>
{
    public GameDecision<DailyLotteryState,DailyBuyResult> Decide(GameActionInput<DailyLotteryState,DailyBuyCommand> input)
    {
        var c=input.Command;var row=input.State.Row;var owned=input.State.Tickets.Count(x=>x.UserId==c.UserId);
        if(c.Count<=0)return Reject(input.State,new(DailyBuyStatus.InvalidCount,null,0,0,0,0,0),"invalid_count");
        if(c.Count>Math.Max(1,c.PerCommandCap))return Reject(input.State,new(DailyBuyStatus.OverPerCommandCap,null,0,0,0,0,0),"command_cap");
        if(!string.Equals(row.Status,"open",StringComparison.Ordinal)||row.DeadlineAt<=input.UtcNow.UtcDateTime)return Reject(input.State,new(DailyBuyStatus.DayAlreadyClosing,row,0,0,0,0,0),"closing");
        if(c.UserCap>0&&owned+c.Count>c.UserCap)return Reject(input.State,new(DailyBuyStatus.OverDailyCap,row,0,owned,0,0,0),"daily_cap");
        var cost=checked(c.Count*row.TicketPrice);if(cost>input.Wallet.Balance)return Reject(input.State,new(DailyBuyStatus.NotEnoughCoins,row,0,owned,0,0,(int)input.Wallet.Balance),"insufficient_balance");
        var tickets=input.State.Tickets.Concat(Enumerable.Repeat(new DailyTicketOwner(c.UserId,c.DisplayName),c.Count)).ToArray();
        return new(DecisionStatus.Accepted,new(row,tickets),new(DailyBuyStatus.Ok,row,c.Count,owned+c.Count,tickets.Length,tickets.Length*row.TicketPrice,(int)input.Wallet.Balance-cost),[EconomyEffect.Debit(cost,"pick.daily.buy")],[],[],[new PickDailyTicketsBought(row.Id,c.UserId,c.ChatId,c.Count,cost,input.UtcNow.ToUnixTimeMilliseconds())],[]);
    }
    private static GameDecision<DailyLotteryState,DailyBuyResult> Reject(DailyLotteryState s,DailyBuyResult r,string reason)=>new(DecisionStatus.Rejected,s,r,[],[],[],[],[],reason);
}
