using BotFramework.Host.Execution;
using Games.Pick.Application.Execution;

namespace Games.Pick.Infrastructure.Persistence;

public sealed class DailyBuyStateStore : IGameStateStore<DailyBuyCommand, DailyLotteryState>
{
    public async Task<DailyLotteryState> LoadAsync(
        DailyBuyCommand command,
        IGameExecutionContext context,
        CancellationToken ct)
    {
        var id = Guid.NewGuid();
        await context.ExecuteAsync(
            "INSERT INTO pick_daily_lottery (id,chat_id,day_local,ticket_price,status,deadline_at) VALUES (@id,@ChatId,@day,@TicketPrice,'open',@DeadlineUtc) ON CONFLICT (chat_id,day_local) DO NOTHING",
            new
            {
                id,
                command.ChatId,
                day = command.DayLocal.ToDateTime(TimeOnly.MinValue),
                command.TicketPrice,
                command.DeadlineUtc,
            },
            ct);
        var row = await DailySql.Row(command.ChatId, command.DayLocal, context, ct)
            ?? throw new InvalidOperationException("Daily row missing.");
        return new(row, await DailySql.Tickets(row.Id, context, ct));
    }

    public async Task SaveAsync(
        DailyBuyCommand command,
        DailyLotteryState state,
        IGameExecutionContext context,
        CancellationToken ct)
    {
        var inserted = await context.ExecuteAsync(
            "INSERT INTO pick_daily_lottery_tickets (lottery_id,user_id,display_name,price_paid) SELECT @id,@UserId,@DisplayName,@price FROM generate_series(1,@Count)",
            new
            {
                id = state.Row.Id,
                command.UserId,
                command.DisplayName,
                price = state.Row.TicketPrice,
                command.Count,
            },
            ct);
        if (inserted != command.Count)
            throw new InvalidOperationException("Ticket insert mismatch.");
    }
}
