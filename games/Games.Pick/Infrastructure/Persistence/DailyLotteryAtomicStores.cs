using System.Text.Json;
using BotFramework.Host.Execution;
using Games.Pick.Application.Execution;
namespace Games.Pick.Infrastructure.Persistence;

public sealed class DailyBuyStateStore:IGameStateStore<DailyBuyCommand,DailyLotteryState>
{
 public async Task<DailyLotteryState> LoadAsync(DailyBuyCommand c,IGameExecutionContext x,CancellationToken ct){var id=Guid.NewGuid();await x.ExecuteAsync("INSERT INTO pick_daily_lottery (id,chat_id,day_local,ticket_price,status,deadline_at) VALUES (@id,@ChatId,@day,@TicketPrice,'open',@DeadlineUtc) ON CONFLICT (chat_id,day_local) DO NOTHING",new{id,c.ChatId,day=c.DayLocal.ToDateTime(TimeOnly.MinValue),c.TicketPrice,c.DeadlineUtc},ct);var row=await DailySql.Row(c.ChatId,c.DayLocal,x,ct)??throw new InvalidOperationException("Daily row missing.");return new(row,await DailySql.Tickets(row.Id,x,ct));}
 public async Task SaveAsync(DailyBuyCommand c,DailyLotteryState s,IGameExecutionContext x,CancellationToken ct){var n=await x.ExecuteAsync("INSERT INTO pick_daily_lottery_tickets (lottery_id,user_id,display_name,price_paid) SELECT @id,@UserId,@DisplayName,@price FROM generate_series(1,@Count)",new{id=s.Row.Id,c.UserId,c.DisplayName,price=s.Row.TicketPrice,c.Count},ct);if(n!=c.Count)throw new InvalidOperationException("Ticket insert mismatch.");}
}
public sealed class DailySettleStateStore:IGameStateStore<DailySettleCommand,DailyLotteryState>
{
 public async Task<DailyLotteryState> LoadAsync(DailySettleCommand c,IGameExecutionContext x,CancellationToken ct){var row=await DailySql.ById(c.Row.Id,x,ct)??throw new InvalidOperationException("Daily lottery already settled.");var tickets=await DailySql.Tickets(row.Id,x,ct);if(!tickets.GroupBy(z=>z.UserId).ToDictionary(z=>z.Key,z=>z.Count()).OrderBy(z=>z.Key).SequenceEqual(c.ExpectedTickets.GroupBy(z=>z.UserId).ToDictionary(z=>z.Key,z=>z.Count()).OrderBy(z=>z.Key)))throw new InvalidOperationException("Daily tickets changed before settlement lock.");return new(row,tickets);}
 public async Task SaveAsync(DailySettleCommand c,DailyLotteryState s,IGameExecutionContext x,CancellationToken ct){var r=s.Row;var n=await x.ExecuteAsync("UPDATE pick_daily_lottery SET status=@Status,settled_at=@SettledAt,winner_id=@WinnerId,winner_name=@WinnerName,ticket_count=@TicketCount,pot_total=@PotTotal,payout=@Payout,fee=@Fee WHERE id=@Id AND status='open'",r,ct);if(n!=1)throw new InvalidOperationException("Daily lottery already settled.");}
}
internal static class DailySql
{
 private const string S="SELECT id AS Id,chat_id AS ChatId,day_local AS DayLocal,ticket_price AS TicketPrice,status AS Status,opened_at AS OpenedAt,deadline_at AS DeadlineAt,settled_at AS SettledAt,winner_id AS WinnerId,winner_name AS WinnerName,ticket_count AS TicketCount,pot_total AS PotTotal,payout AS Payout,fee AS Fee FROM pick_daily_lottery";
 public static Task<PickDailyLotteryRow?> Row(long chat,DateOnly day,IGameExecutionContext x,CancellationToken ct)=>x.QuerySingleOrDefaultAsync<PickDailyLotteryRow>($"{S} WHERE chat_id=@chat AND day_local=@d FOR UPDATE",new{chat,d=day.ToDateTime(TimeOnly.MinValue)},ct);
 public static Task<PickDailyLotteryRow?> ById(Guid id,IGameExecutionContext x,CancellationToken ct)=>x.QuerySingleOrDefaultAsync<PickDailyLotteryRow>($"{S} WHERE id=@id AND status='open' FOR UPDATE",new{id},ct);
 public static async Task<IReadOnlyList<DailyTicketOwner>> Tickets(Guid id,IGameExecutionContext x,CancellationToken ct){var json=await x.QuerySingleOrDefaultAsync<string>("SELECT COALESCE(json_agg(json_build_object('UserId',user_id,'DisplayName',display_name) ORDER BY id)::text,'[]') FROM pick_daily_lottery_tickets WHERE lottery_id=@id",new{id},ct);return JsonSerializer.Deserialize<DailyTicketOwner[]>(json??"[]")??[];}
}
