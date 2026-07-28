using BotFramework.Host.Execution;
using Games.Pick.Application.Execution;

namespace Games.Pick.Infrastructure.Persistence;

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
