using System.Text.Json;
using BotFramework.Host.Execution;
using Games.Pick.Application.Execution;
namespace Games.Pick.Infrastructure.Persistence;

internal static class DailySql
{
 private const string S="SELECT id AS Id,chat_id AS ChatId,day_local AS DayLocal,ticket_price AS TicketPrice,status AS Status,opened_at AS OpenedAt,deadline_at AS DeadlineAt,settled_at AS SettledAt,winner_id AS WinnerId,winner_name AS WinnerName,ticket_count AS TicketCount,pot_total AS PotTotal,payout AS Payout,fee AS Fee FROM pick_daily_lottery";
 public static Task<PickDailyLotteryRow?> Row(long chat,DateOnly day,IGameExecutionContext x,CancellationToken ct)=>x.QuerySingleOrDefaultAsync<PickDailyLotteryRow>($"{S} WHERE chat_id=@chat AND day_local=@d FOR UPDATE",new{chat,d=day.ToDateTime(TimeOnly.MinValue)},ct);
 public static Task<PickDailyLotteryRow?> ById(Guid id,IGameExecutionContext x,CancellationToken ct)=>x.QuerySingleOrDefaultAsync<PickDailyLotteryRow>($"{S} WHERE id=@id AND status='open' FOR UPDATE",new{id},ct);
 public static async Task<IReadOnlyList<DailyTicketOwner>> Tickets(Guid id,IGameExecutionContext x,CancellationToken ct){var json=await x.QuerySingleOrDefaultAsync<string>("SELECT COALESCE(json_agg(json_build_object('UserId',user_id,'DisplayName',display_name) ORDER BY id)::text,'[]') FROM pick_daily_lottery_tickets WHERE lottery_id=@id",new{id},ct);return JsonSerializer.Deserialize<DailyTicketOwner[]>(json??"[]")??[];}
}
