using BotFramework.Host.Execution;
using Games.DiceCube.Application.Execution;

namespace Games.DiceCube.Infrastructure.Persistence;

public sealed class DiceCubeBetStateStore :
    IGameStateStore<DiceCubePlaceBetCommand, DiceCubePlaceBetState>,
    IGameStateStore<DiceCubeRollCommand, DiceCubePlaceBetState>,
    IGameStateStore<DiceCubeAbortCommand, DiceCubePlaceBetState>
{
    public Task<DiceCubePlaceBetState> LoadAsync(
        DiceCubePlaceBetCommand command,
        IGameExecutionContext context,
        CancellationToken ct) => LoadAsync(command.UserId, command.ChatId, context, ct);

    public Task<DiceCubePlaceBetState> LoadAsync(
        DiceCubeRollCommand command,
        IGameExecutionContext context,
        CancellationToken ct) => LoadAsync(command.UserId, command.ChatId, context, ct);

    public Task<DiceCubePlaceBetState> LoadAsync(
        DiceCubeAbortCommand command,
        IGameExecutionContext context,
        CancellationToken ct) => LoadAsync(command.UserId, command.ChatId, context, ct);

    public Task SaveAsync(
        DiceCubePlaceBetCommand command,
        DiceCubePlaceBetState state,
        IGameExecutionContext context,
        CancellationToken ct) => SaveAsync(command.UserId, command.ChatId, state, context, ct);

    public Task SaveAsync(
        DiceCubeRollCommand command,
        DiceCubePlaceBetState state,
        IGameExecutionContext context,
        CancellationToken ct) => SaveAsync(command.UserId, command.ChatId, state, context, ct);

    public Task SaveAsync(
        DiceCubeAbortCommand command,
        DiceCubePlaceBetState state,
        IGameExecutionContext context,
        CancellationToken ct) => SaveAsync(command.UserId, command.ChatId, state, context, ct);

    private static async Task<DiceCubePlaceBetState> LoadAsync(
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
                   created_at AS CreatedAt,
                   mult4 AS Mult4,
                   mult5 AS Mult5,
                   mult6 AS Mult6
            FROM dicecube_bets
            WHERE user_id = @userId AND chat_id = @chatId
            FOR UPDATE
            """,
            new { userId, chatId },
            ct).ConfigureAwait(false);
        var pending = row is null
            ? null
            : new DiceCubePendingBet(
                row.UserId,
                row.ChatId,
                row.Amount,
                new DateTimeOffset(DateTime.SpecifyKind(row.CreatedAt, DateTimeKind.Utc)),
                row.Mult4,
                row.Mult5,
                row.Mult6);
        return new DiceCubePlaceBetState(pending);
    }

    private static async Task SaveAsync(
        long userId,
        long chatId,
        DiceCubePlaceBetState state,
        IGameExecutionContext context,
        CancellationToken ct)
    {
        if (state.PendingBet is { } pending)
        {
            var affected = await context.ExecuteAsync(
                """
                INSERT INTO dicecube_bets (user_id, chat_id, amount, created_at, mult4, mult5, mult6)
                VALUES (@UserId, @ChatId, @Amount, @CreatedAt, @Mult4, @Mult5, @Mult6)
                """,
                pending,
                ct).ConfigureAwait(false);
            if (affected != 1)
                throw new InvalidOperationException("DiceCube pending bet was not inserted.");

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
                    gameId = MiniGameIds.DiceCube,
                    expiresAt = pending.CreatedAt.AddMilliseconds(BotMiniGameSession.TtlMs),
                },
                ct).ConfigureAwait(false);
            return;
        }

        await context.ExecuteAsync(
            "DELETE FROM dicecube_bets WHERE user_id = @userId AND chat_id = @chatId",
            new { userId, chatId },
            ct).ConfigureAwait(false);
        await context.ExecuteAsync(
            """
            DELETE FROM mini_game_sessions
            WHERE user_id = @userId AND chat_id = @chatId AND game_id = @gameId
            """,
            new { userId, chatId, gameId = MiniGameIds.DiceCube },
            ct).ConfigureAwait(false);
    }

    private sealed record PendingBetRow(
        long UserId,
        long ChatId,
        int Amount,
        DateTime CreatedAt,
        int Mult4,
        int Mult5,
        int Mult6);
}
