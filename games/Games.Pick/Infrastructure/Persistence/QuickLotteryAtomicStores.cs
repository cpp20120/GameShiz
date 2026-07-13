using BotFramework.Host.Execution;
using Games.Pick.Application.Execution;

namespace Games.Pick.Infrastructure.Persistence;

public sealed class QuickLotteryOpenStateStore : IGameStateStore<QuickLotteryOpenCommand, QuickLotteryState>
{
    public async Task<QuickLotteryState> LoadAsync(QuickLotteryOpenCommand c, IGameExecutionContext x, CancellationToken ct) => new(await LotterySql.Open(c.ChatId, x, ct), []);
    public async Task SaveAsync(QuickLotteryOpenCommand c, QuickLotteryState s, IGameExecutionContext x, CancellationToken ct)
    {
        var row = s.Row!; var entry = s.Entries.Single();
        var n = await x.ExecuteAsync("""
            INSERT INTO pick_lottery (id,chat_id,opener_id,opener_name,stake,status,opened_at,deadline_at)
            VALUES (@Id,@ChatId,@OpenerId,@OpenerName,@Stake,'open',@OpenedAt,@DeadlineAt)
            ON CONFLICT (chat_id) WHERE status='open' DO NOTHING
            """, row, ct);
        if (n != 1) throw new InvalidOperationException("Concurrent quick lottery open.");
        await x.ExecuteAsync("INSERT INTO pick_lottery_entries (lottery_id,user_id,display_name,stake_paid,entered_at) VALUES (@LotteryId,@UserId,@DisplayName,@StakePaid,@EnteredAt)", entry, ct);
    }
}

public sealed class QuickLotteryJoinStateStore : IGameStateStore<QuickLotteryJoinCommand, QuickLotteryState>
{
    public async Task<QuickLotteryState> LoadAsync(QuickLotteryJoinCommand c, IGameExecutionContext x, CancellationToken ct)
    { var row = await LotterySql.Open(c.ChatId, x, ct); return new(row, row is null ? [] : await LotterySql.Entries(row.Id, x, ct)); }
    public async Task SaveAsync(QuickLotteryJoinCommand c, QuickLotteryState s, IGameExecutionContext x, CancellationToken ct)
    {
        var entry = s.Entries.Single(e => e.UserId == c.UserId);
        var n = await x.ExecuteAsync("INSERT INTO pick_lottery_entries (lottery_id,user_id,display_name,stake_paid,entered_at) VALUES (@LotteryId,@UserId,@DisplayName,@StakePaid,@EnteredAt) ON CONFLICT DO NOTHING", entry, ct);
        if (n != 1) throw new InvalidOperationException("Concurrent quick lottery join.");
    }
}

public sealed class QuickLotterySettleStateStore : IGameStateStore<QuickLotterySettleCommand, QuickLotteryState>
{
    public async Task<QuickLotteryState> LoadAsync(QuickLotterySettleCommand c, IGameExecutionContext x, CancellationToken ct)
    { var row = await LotterySql.ById(c.Row.Id, x, ct); var entries = row is null ? [] : await LotterySql.Entries(row.Id, x, ct); if (!Same(entries, c.ExpectedEntries)) throw new InvalidOperationException("Lottery entries changed before settlement lock."); return new(row, entries); }
    public async Task SaveAsync(QuickLotterySettleCommand c, QuickLotteryState s, IGameExecutionContext x, CancellationToken ct)
    {
        var row = s.Row!;
        var n = await x.ExecuteAsync("""
            UPDATE pick_lottery SET status=@Status, settled_at=@SettledAt, winner_id=@WinnerId,
              winner_name=@WinnerName, pot_total=@PotTotal, payout=@Payout, fee=@Fee
            WHERE id=@Id AND status='open'
            """, row, ct);
        if (n != 1) throw new InvalidOperationException("Lottery was already settled.");
    }
    private static bool Same(IReadOnlyList<PickLotteryEntryRow> a, IReadOnlyList<PickLotteryEntryRow> b) => a.Select(x => x.UserId).Order().SequenceEqual(b.Select(x => x.UserId).Order());
}

internal static class LotterySql
{
    private const string Select = "SELECT id AS Id,chat_id AS ChatId,opener_id AS OpenerId,opener_name AS OpenerName,stake AS Stake,status AS Status,opened_at AS OpenedAt,deadline_at AS DeadlineAt,settled_at AS SettledAt,winner_id AS WinnerId,winner_name AS WinnerName,pot_total AS PotTotal,payout AS Payout,fee AS Fee FROM pick_lottery";
    public static Task<PickLotteryRow?> Open(long chatId, IGameExecutionContext x, CancellationToken ct) => x.QuerySingleOrDefaultAsync<PickLotteryRow>($"{Select} WHERE chat_id=@chatId AND status='open' FOR UPDATE", new { chatId }, ct);
    public static Task<PickLotteryRow?> ById(Guid id, IGameExecutionContext x, CancellationToken ct) => x.QuerySingleOrDefaultAsync<PickLotteryRow>($"{Select} WHERE id=@id AND status='open' FOR UPDATE", new { id }, ct);
    public static async Task<IReadOnlyList<PickLotteryEntryRow>> Entries(Guid id, IGameExecutionContext x, CancellationToken ct)
    {
        var json = await x.QuerySingleOrDefaultAsync<string>("SELECT COALESCE(json_agg(json_build_object('LotteryId',lottery_id,'UserId',user_id,'DisplayName',display_name,'StakePaid',stake_paid,'EnteredAt',entered_at) ORDER BY entered_at)::text,'[]') FROM pick_lottery_entries WHERE lottery_id=@id", new { id }, ct);
        return System.Text.Json.JsonSerializer.Deserialize<PickLotteryEntryRow[]>(json ?? "[]") ?? [];
    }
}
