using BotFramework.Host.Execution;
using Games.Pick.Application.Execution;

namespace Games.Pick.Infrastructure.Persistence;

public sealed class DailySettleStateStore : IGameStateStore<DailySettleCommand, DailyLotteryState>
{
    public async Task<DailyLotteryState> LoadAsync(
        DailySettleCommand command,
        IGameExecutionContext context,
        CancellationToken ct)
    {
        var row = await DailySql.ById(command.Row.Id, context, ct)
            ?? throw new InvalidOperationException("Daily lottery already settled.");
        var tickets = await DailySql.Tickets(row.Id, context, ct);
        var actual = tickets
            .GroupBy(ticket => ticket.UserId)
            .ToDictionary(group => group.Key, group => group.Count())
            .OrderBy(pair => pair.Key);
        var expected = command.ExpectedTickets
            .GroupBy(ticket => ticket.UserId)
            .ToDictionary(group => group.Key, group => group.Count())
            .OrderBy(pair => pair.Key);
        if (!actual.SequenceEqual(expected))
            throw new InvalidOperationException("Daily tickets changed before settlement lock.");
        return new(row, tickets);
    }

    public async Task SaveAsync(
        DailySettleCommand command,
        DailyLotteryState state,
        IGameExecutionContext context,
        CancellationToken ct)
    {
        var row = state.Row;
        var updated = await context.ExecuteAsync(
            "UPDATE pick_daily_lottery SET status=@Status,settled_at=@SettledAt,winner_id=@WinnerId,winner_name=@WinnerName,ticket_count=@TicketCount,pot_total=@PotTotal,payout=@Payout,fee=@Fee WHERE id=@Id AND status='open'",
            row,
            ct);
        if (updated != 1)
            throw new InvalidOperationException("Daily lottery already settled.");
    }
}
