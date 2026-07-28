using BotFramework.Host.Execution;
using Games.Pick.Application.Execution;

namespace Games.Pick.Infrastructure.Persistence;

public sealed class QuickLotteryOpenStateStore : IGameStateStore<QuickLotteryOpenCommand, QuickLotteryState>
{
    public async Task<QuickLotteryState> LoadAsync(
        QuickLotteryOpenCommand command,
        IGameExecutionContext context,
        CancellationToken ct) =>
        new(await LotterySql.Open(command.ChatId, context, ct), []);

    public async Task SaveAsync(
        QuickLotteryOpenCommand command,
        QuickLotteryState state,
        IGameExecutionContext context,
        CancellationToken ct)
    {
        var row = state.Row ?? throw new InvalidOperationException("Lottery row is missing.");
        var entry = state.Entries.Single();
        var inserted = await context.ExecuteAsync(
            """
            INSERT INTO pick_lottery (id,chat_id,opener_id,opener_name,stake,status,opened_at,deadline_at)
            VALUES (@Id,@ChatId,@OpenerId,@OpenerName,@Stake,'open',@OpenedAt,@DeadlineAt)
            ON CONFLICT (chat_id) WHERE status='open' DO NOTHING
            """,
            row,
            ct);
        if (inserted != 1)
            throw new InvalidOperationException("Concurrent quick lottery open.");
        await context.ExecuteAsync(
            "INSERT INTO pick_lottery_entries (lottery_id,user_id,display_name,stake_paid,entered_at) VALUES (@LotteryId,@UserId,@DisplayName,@StakePaid,@EnteredAt)",
            entry,
            ct);
    }
}
