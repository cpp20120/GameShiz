using BotFramework.Sdk.Execution;
using Games.Pick.Domain.Events;

namespace Games.Pick.Application.Execution;

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
