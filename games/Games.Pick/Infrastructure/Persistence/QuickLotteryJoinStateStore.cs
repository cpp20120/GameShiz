using BotFramework.Host.Execution;
using Games.Pick.Application.Execution;

namespace Games.Pick.Infrastructure.Persistence;

public sealed class QuickLotteryJoinStateStore : IGameStateStore<QuickLotteryJoinCommand, QuickLotteryState>
{
    public async Task<QuickLotteryState> LoadAsync(
        QuickLotteryJoinCommand command,
        IGameExecutionContext context,
        CancellationToken ct)
    {
        var row = await LotterySql.Open(command.ChatId, context, ct);
        return new(row, row is null ? [] : await LotterySql.Entries(row.Id, context, ct));
    }

    public async Task SaveAsync(
        QuickLotteryJoinCommand command,
        QuickLotteryState state,
        IGameExecutionContext context,
        CancellationToken ct)
    {
        var entry = state.Entries.Single(item => item.UserId == command.UserId);
        var inserted = await context.ExecuteAsync(
            "INSERT INTO pick_lottery_entries (lottery_id,user_id,display_name,stake_paid,entered_at) VALUES (@LotteryId,@UserId,@DisplayName,@StakePaid,@EnteredAt) ON CONFLICT DO NOTHING",
            entry,
            ct);
        if (inserted != 1)
            throw new InvalidOperationException("Concurrent quick lottery join.");
    }
}
