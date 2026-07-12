using BotFramework.Host.Execution;
using Games.Football.Application.Execution;

namespace Games.Football.Infrastructure.Persistence;

public sealed class FootballBetStateStore :
    IGameStateStore<FootballPlaceBetCommand, FootballBetState>,
    IGameStateStore<FootballThrowCommand, FootballBetState>,
    IGameStateStore<FootballAbortCommand, FootballBetState>
{
    public Task<FootballBetState> LoadAsync(FootballPlaceBetCommand command, IGameExecutionContext context, CancellationToken ct) => Load(command.UserId, command.ChatId, context, ct);
    public Task<FootballBetState> LoadAsync(FootballThrowCommand command, IGameExecutionContext context, CancellationToken ct) => Load(command.UserId, command.ChatId, context, ct);
    public Task<FootballBetState> LoadAsync(FootballAbortCommand command, IGameExecutionContext context, CancellationToken ct) => Load(command.UserId, command.ChatId, context, ct);
    public Task SaveAsync(FootballPlaceBetCommand command, FootballBetState state, IGameExecutionContext context, CancellationToken ct) => Save(command.UserId, command.ChatId, state, context, ct);
    public Task SaveAsync(FootballThrowCommand command, FootballBetState state, IGameExecutionContext context, CancellationToken ct) => Save(command.UserId, command.ChatId, state, context, ct);
    public Task SaveAsync(FootballAbortCommand command, FootballBetState state, IGameExecutionContext context, CancellationToken ct) => Save(command.UserId, command.ChatId, state, context, ct);

    private static async Task<FootballBetState> Load(long userId, long chatId, IGameExecutionContext context, CancellationToken ct)
    {
        var row = await context.QuerySingleOrDefaultAsync<Row>(
            """
            SELECT user_id AS UserId, chat_id AS ChatId, amount AS Amount, created_at AS CreatedAt
            FROM football_bets WHERE user_id = @userId AND chat_id = @chatId FOR UPDATE
            """, new { userId, chatId }, ct).ConfigureAwait(false);
        return new(row is null ? null : new(
            row.UserId, row.ChatId, row.Amount,
            new DateTimeOffset(DateTime.SpecifyKind(row.CreatedAt, DateTimeKind.Utc))));
    }

    private static async Task Save(long userId, long chatId, FootballBetState state, IGameExecutionContext context, CancellationToken ct)
    {
        if (state.PendingBet is { } pending)
        {
            await context.ExecuteAsync(
                "INSERT INTO football_bets (user_id, chat_id, amount, created_at) VALUES (@UserId, @ChatId, @Amount, @CreatedAt)",
                pending, ct).ConfigureAwait(false);
            await context.ExecuteAsync(
                """
                INSERT INTO mini_game_sessions (user_id, chat_id, game_id, expires_at, updated_at)
                VALUES (@userId, @chatId, @gameId, @expiresAt, now())
                ON CONFLICT (user_id, chat_id) DO UPDATE SET
                    game_id = EXCLUDED.game_id, expires_at = EXCLUDED.expires_at, updated_at = now()
                """,
                new
                {
                    userId, chatId, gameId = MiniGameIds.Football,
                    expiresAt = pending.CreatedAt.AddMilliseconds(BotMiniGameSession.TtlMs),
                }, ct).ConfigureAwait(false);
            return;
        }
        await context.ExecuteAsync("DELETE FROM football_bets WHERE user_id = @userId AND chat_id = @chatId", new { userId, chatId }, ct).ConfigureAwait(false);
        await context.ExecuteAsync(
            "DELETE FROM mini_game_sessions WHERE user_id = @userId AND chat_id = @chatId AND game_id = @gameId",
            new { userId, chatId, gameId = MiniGameIds.Football }, ct).ConfigureAwait(false);
    }

    private sealed record Row(long UserId, long ChatId, int Amount, DateTime CreatedAt);
}
