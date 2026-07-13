using BotFramework.Host.Execution;
using Games.Pick.Application.Execution;
using Microsoft.Extensions.Options;
namespace Games.Pick.Application.Services;
public sealed class PickDailyLotteryService(IPickDailyLotteryStore store,IAtomicGameExecutor<DailyBuyCommand,DailyLotteryState,DailyBuyResult> buy,IAtomicGameExecutor<DailySettleCommand,DailyLotteryState,DailySettleResult> settle,IOptions<PickOptions> pickOptions,IOptions<TelegramDiceDailyLimitOptions> dice):IPickDailyLotteryService
{
 private PickDailyLotteryOptions O=>pickOptions.Value.Daily;public int OffsetHours=>O.TimezoneOffsetHoursOverride!=0?O.TimezoneOffsetHoursOverride:dice.Value.TimezoneOffsetHours;public int DrawHourLocal=>Math.Clamp(O.DrawHourLocal,0,23);
 public DateOnly LocalToday(){var offset=TimeSpan.FromHours(OffsetHours);var now=DateTimeOffset.UtcNow.ToOffset(offset);var draw=new DateTimeOffset(now.Year,now.Month,now.Day,DrawHourLocal,0,0,offset);return DateOnly.FromDateTime(now<=draw?now.Date:now.Date.AddDays(1));}
 public DateTime LocalNextDrawUtc(){var d=LocalToday();return new DateTimeOffset(d.Year,d.Month,d.Day,DrawHourLocal,0,0,TimeSpan.FromHours(OffsetHours)).UtcDateTime;}
 public Task<DailyBuyResult> BuyAsync(long u,string n,long ch,int count,CancellationToken ct)=>BuyAsync(u,n,ch,count,0,ct);
 public Task<DailyBuyResult> BuyAsync(long u,string n,long ch,int count,int source,CancellationToken ct){var o=O;var day=LocalToday();var id=source!=0?$"pick:daily:buy:{ch}:{source}:{u}":$"pick:daily:buy:legacy:{Guid.NewGuid():N}";return buy.ExecuteAsync(new(new(u,n,ch,count,id,day,LocalNextDrawUtc(),Math.Max(1,o.TicketPrice),o.MaxTicketsPerBuyCommand,o.MaxTicketsPerUserPerDay)),ct);}
 public async Task<DailyInfoSnapshot?> InfoAsync(long ch,long viewer,CancellationToken ct){var row=await store.FindOpenByChatAsync(ch,LocalToday(),ct);if(row is null)return null;var s=await store.ListUserTicketCountsAsync(row.Id,ct);var total=s.Sum(x=>x.TicketCount);return new(row,total,s.Count,total*row.TicketPrice,s.FirstOrDefault(x=>x.UserId==viewer)?.TicketCount??0,s.Take(10).ToList());}
 public async Task<DailySettleResult> SettleAsync(PickDailyLotteryRow row,CancellationToken ct){var s=await store.ListUserTicketCountsAsync(row.Id,ct);var tickets=s.SelectMany(x=>Enumerable.Repeat(new DailyTicketOwner(x.UserId,x.DisplayName),x.TicketCount)).ToArray();return await settle.ExecuteAsync(new(new(row,tickets,$"pick:daily:settle:{row.Id:N}",O.HouseFeePercent)),ct);}
 public Task<IReadOnlyList<PickDailyLotteryRow>> HistoryAsync(long ch,int limit,CancellationToken ct)=>store.ListHistoryAsync(ch,Math.Clamp(limit,1,30),ct);
}
