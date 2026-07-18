using BotFramework.Host.Execution;
using Games.Bowling.Application.Execution;

namespace Games.Bowling.Infrastructure.Persistence;

public sealed class BowlingBetStateStore :
    IGameStateStore<BowlingPlaceBetCommand, BowlingBetState>,
    IGameStateStore<BowlingRollCommand, BowlingBetState>,
    IGameStateStore<BowlingAbortCommand, BowlingBetState>
{
    public Task<BowlingBetState> LoadAsync(BowlingPlaceBetCommand command, IGameExecutionContext context, CancellationToken ct) => Load(command.UserId, command.ChatId, context, ct);
    public Task<BowlingBetState> LoadAsync(BowlingRollCommand command, IGameExecutionContext context, CancellationToken ct) => Load(command.UserId, command.ChatId, context, ct);
    public Task<BowlingBetState> LoadAsync(BowlingAbortCommand command, IGameExecutionContext context, CancellationToken ct) => Load(command.UserId, command.ChatId, context, ct);
    public Task SaveAsync(BowlingPlaceBetCommand command, BowlingBetState state, IGameExecutionContext context, CancellationToken ct) => Save(command.UserId, command.ChatId, state, context, ct);
    public Task SaveAsync(BowlingRollCommand command, BowlingBetState state, IGameExecutionContext context, CancellationToken ct) => Save(command.UserId, command.ChatId, state, context, ct);
    public Task SaveAsync(BowlingAbortCommand command, BowlingBetState state, IGameExecutionContext context, CancellationToken ct) => Save(command.UserId, command.ChatId, state, context, ct);

    private static async Task<BowlingBetState> Load(long userId, long chatId, IGameExecutionContext context, CancellationToken ct)
    {
        var row = await context.QuerySingleOrDefaultAsync<Row>(
            """
            SELECT user_id AS UserId, chat_id AS ChatId, amount AS Amount, created_at AS CreatedAt
            FROM bowling_bets WHERE user_id = @userId AND chat_id = @chatId FOR UPDATE
            """,
            new { userId, chatId }, ct).ConfigureAwait(false);
        return new(row is null ? null : new(
            row.UserId, row.ChatId, row.Amount,
            new DateTimeOffset(DateTime.SpecifyKind(row.CreatedAt, DateTimeKind.Utc))));
    }

    private static async Task Save(long userId, long chatId, BowlingBetState state, IGameExecutionContext context, CancellationToken ct)
    {
        if (state.PendingBet is { } pending)
        {
            await context.ExecuteAsync(
                "INSERT INTO bowling_bets (user_id, chat_id, amount, created_at) VALUES (@UserId, @ChatId, @Amount, @CreatedAt)",
                pending, ct).ConfigureAwait(false);
            await SaveSession(userId, chatId, pending.CreatedAt, context, ct).ConfigureAwait(false);
            return;
        }
        await context.ExecuteAsync("DELETE FROM bowling_bets WHERE user_id = @userId AND chat_id = @chatId", new { userId, chatId }, ct).ConfigureAwait(false);
        await context.ExecuteAsync(
            "DELETE FROM mini_game_sessions WHERE user_id = @userId AND chat_id = @chatId AND game_id = @gameId",
            new { userId, chatId, gameId = MiniGameIds.Bowling }, ct).ConfigureAwait(false);
    }

    private static Task<int> SaveSession(long userId, long chatId, DateTimeOffset createdAt, IGameExecutionContext context, CancellationToken ct) =>
        context.ExecuteAsync(
            """
            INSERT INTO mini_game_sessions (user_id, chat_id, game_id, expires_at, updated_at)
            VALUES (@userId, @chatId, @gameId, @expiresAt, now())
            ON CONFLICT (user_id, chat_id, game_id) DO UPDATE SET
                expires_at = EXCLUDED.expires_at, updated_at = now()
            """,
            new { userId, chatId, gameId = MiniGameIds.Bowling, expiresAt = createdAt.AddMilliseconds(BotMiniGameSession.TtlMs) }, ct);

    private sealed record Row(long UserId, long ChatId, int Amount, DateTime CreatedAt);
}
