using BotFramework.Host.Execution;
using Games.Pick.Application.Execution;

namespace Games.Pick.Infrastructure.Persistence;

public sealed class QuickLotterySettleStateStore : IGameStateStore<QuickLotterySettleCommand, QuickLotteryState>
{
    public async Task<QuickLotteryState> LoadAsync(
        QuickLotterySettleCommand command,
        IGameExecutionContext context,
        CancellationToken ct)
    {
        var row = await LotterySql.ById(command.Row.Id, context, ct);
        var entries = row is null ? [] : await LotterySql.Entries(row.Id, context, ct);
        if (!Same(entries, command.ExpectedEntries))
            throw new InvalidOperationException("Lottery entries changed before settlement lock.");
        return new(row, entries);
    }

    public async Task SaveAsync(
        QuickLotterySettleCommand command,
        QuickLotteryState state,
        IGameExecutionContext context,
        CancellationToken ct)
    {
        var row = state.Row ?? throw new InvalidOperationException("Lottery row is missing.");
        var updated = await context.ExecuteAsync(
            """
            UPDATE pick_lottery SET status=@Status, settled_at=@SettledAt, winner_id=@WinnerId,
              winner_name=@WinnerName, pot_total=@PotTotal, payout=@Payout, fee=@Fee
            WHERE id=@Id AND status='open'
            """,
            row,
            ct);
        if (updated != 1)
            throw new InvalidOperationException("Lottery was already settled.");
    }

    private static bool Same(
        IReadOnlyList<PickLotteryEntryRow> actual,
        IReadOnlyList<PickLotteryEntryRow> expected) =>
        actual.Select(entry => entry.UserId).Order()
            .SequenceEqual(expected.Select(entry => entry.UserId).Order());
}
