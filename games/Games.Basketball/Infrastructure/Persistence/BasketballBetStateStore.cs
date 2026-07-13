using BotFramework.Host.Execution;
using Games.Basketball.Application.Execution;

namespace Games.Basketball.Infrastructure.Persistence;

public sealed class BasketballBetStateStore :
    IGameStateStore<BasketballPlaceBetCommand, BasketballBetState>,
    IGameStateStore<BasketballThrowCommand, BasketballBetState>,
    IGameStateStore<BasketballAbortCommand, BasketballBetState>
{
    public Task<BasketballBetState> LoadAsync(
        BasketballPlaceBetCommand command,
        IGameExecutionContext context,
        CancellationToken ct) => LoadAsync(command.UserId, command.ChatId, context, ct);

    public Task<BasketballBetState> LoadAsync(
        BasketballThrowCommand command,
        IGameExecutionContext context,
        CancellationToken ct) => LoadAsync(command.UserId, command.ChatId, context, ct);

    public Task<BasketballBetState> LoadAsync(
        BasketballAbortCommand command,
        IGameExecutionContext context,
        CancellationToken ct) => LoadAsync(command.UserId, command.ChatId, context, ct);

    public Task SaveAsync(
        BasketballPlaceBetCommand command,
        BasketballBetState state,
        IGameExecutionContext context,
        CancellationToken ct) => SaveAsync(command.UserId, command.ChatId, state, context, ct);

    public Task SaveAsync(
        BasketballThrowCommand command,
        BasketballBetState state,
        IGameExecutionContext context,
        CancellationToken ct) => SaveAsync(command.UserId, command.ChatId, state, context, ct);

    public Task SaveAsync(
        BasketballAbortCommand command,
        BasketballBetState state,
        IGameExecutionContext context,
        CancellationToken ct) => SaveAsync(command.UserId, command.ChatId, state, context, ct);

    private static async Task<BasketballBetState> LoadAsync(
        long userId,
        long chatId,
        IGameExecutionContext context,
        CancellationToken ct)
    {
        var row = await context.QuerySingleOrDefaultAsync<PendingBetRow>(
            """
            SELECT user_id AS UserId,
                   chat_id AS ChatId,
                   amount AS Amount,
                   created_at AS CreatedAt
            FROM basketball_bets
            WHERE user_id = @userId AND chat_id = @chatId
            FOR UPDATE
            """,
            new { userId, chatId },
            ct).ConfigureAwait(false);
        return new BasketballBetState(row is null
            ? null
            : new BasketballPendingBet(
                row.UserId,
                row.ChatId,
                row.Amount,
                new DateTimeOffset(DateTime.SpecifyKind(row.CreatedAt, DateTimeKind.Utc))));
    }

    private static async Task SaveAsync(
        long userId,
        long chatId,
        BasketballBetState state,
        IGameExecutionContext context,
        CancellationToken ct)
    {
        if (state.PendingBet is { } pending)
        {
            var affected = await context.ExecuteAsync(
                """
                INSERT INTO basketball_bets (user_id, chat_id, amount, created_at)
                VALUES (@UserId, @ChatId, @Amount, @CreatedAt)
                """,
                pending,
                ct).ConfigureAwait(false);
            if (affected != 1)
                throw new InvalidOperationException("Basketball pending bet was not inserted.");

            await context.ExecuteAsync(
                """
                INSERT INTO mini_game_sessions (user_id, chat_id, game_id, expires_at, updated_at)
                VALUES (@userId, @chatId, @gameId, @expiresAt, now())
                ON CONFLICT (user_id, chat_id) DO UPDATE SET
                    game_id = EXCLUDED.game_id,
                    expires_at = EXCLUDED.expires_at,
                    updated_at = now()
                """,
                new
                {
                    userId,
                    chatId,
                    gameId = MiniGameIds.Basketball,
                    expiresAt = pending.CreatedAt.AddMilliseconds(BotMiniGameSession.TtlMs),
                },
                ct).ConfigureAwait(false);
            return;
        }

        await context.ExecuteAsync(
            "DELETE FROM basketball_bets WHERE user_id = @userId AND chat_id = @chatId",
            new { userId, chatId },
            ct).ConfigureAwait(false);
        await context.ExecuteAsync(
            """
            DELETE FROM mini_game_sessions
            WHERE user_id = @userId AND chat_id = @chatId AND game_id = @gameId
            """,
            new { userId, chatId, gameId = MiniGameIds.Basketball },
            ct).ConfigureAwait(false);
    }

    private sealed record PendingBetRow(long UserId, long ChatId, int Amount, DateTime CreatedAt);
}
