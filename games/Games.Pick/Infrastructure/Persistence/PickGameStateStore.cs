using BotFramework.Host.Execution;
using Games.Pick.Application.Execution;

namespace Games.Pick.Infrastructure.Persistence;

public sealed class PickGameStateStore : IGameStateStore<PickCommand, PickGameState>
{
    public async Task<PickGameState> LoadAsync(
        PickCommand command, IGameExecutionContext context, CancellationToken ct)
    {
        var streak = await context.QuerySingleOrDefaultAsync<int?>(
            "SELECT streak FROM pick_streaks WHERE user_id = @UserId AND chat_id = @ChatId FOR UPDATE",
            command, ct).ConfigureAwait(false);
        return new(streak ?? 0);
    }

    public Task SaveAsync(
        PickCommand command, PickGameState state, IGameExecutionContext context, CancellationToken ct) =>
        state.Streak == 0
            ? context.ExecuteAsync(
                "DELETE FROM pick_streaks WHERE user_id = @UserId AND chat_id = @ChatId",
                command, ct)
            : context.ExecuteAsync(
                """
                INSERT INTO pick_streaks (user_id, chat_id, streak, updated_at)
                VALUES (@UserId, @ChatId, @streak, now())
                ON CONFLICT (user_id, chat_id) DO UPDATE SET streak = EXCLUDED.streak, updated_at = now()
                """,
                new { command.UserId, command.ChatId, state.Streak }, ct);
}
