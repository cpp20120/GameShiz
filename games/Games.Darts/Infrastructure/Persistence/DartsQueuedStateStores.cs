using BotFramework.Contracts.Messaging;
using BotFramework.Host.Execution;
using Games.Darts.Application.Execution;

namespace Games.Darts.Infrastructure.Persistence;

public sealed class DartsPlaceBetStateStore : IGameStateStore<DartsPlaceBetCommand, DartsQueuedState>
{
    public async Task<DartsQueuedState> LoadAsync(
        DartsPlaceBetCommand command, IGameExecutionContext context, CancellationToken ct)
    {
        var queuedAhead = await context.QuerySingleOrDefaultAsync<int>(
            "SELECT COUNT(*)::int FROM darts_rounds WHERE chat_id=@ChatId AND status IN (@Queued,@Awaiting)",
            new { command.ChatId, Queued = (short)DartsRoundStatus.Queued, Awaiting = (short)DartsRoundStatus.AwaitingOutcome }, ct);
        return new(null, queuedAhead);
    }

    public async Task SaveAsync(
        DartsPlaceBetCommand command, DartsQueuedState state, IGameExecutionContext context, CancellationToken ct)
    {
        var round = state.Round ?? throw new InvalidOperationException("Accepted darts bet has no round.");
        var inserted = await context.ExecuteAsync("""
            INSERT INTO darts_rounds (id,user_id,chat_id,amount,created_at,status,bot_message_id,reply_to_message_id,channel)
            VALUES (@Id,@UserId,@ChatId,@Amount,@CreatedAt,@Status,@BotMessageId,@ReplyToMessageId,@Channel)
            ON CONFLICT (id) DO NOTHING
            """, new
        {
            round.Id, round.UserId, round.ChatId, round.Amount, round.CreatedAt,
            Status = (short)round.Status, round.BotMessageId, round.ReplyToMessageId,
            Channel = round.Channel.ToString().ToLowerInvariant(),
        }, ct);
        if (inserted != 1) throw new InvalidOperationException("Darts round id already exists.");
    }
}

public sealed class DartsResolveRoundStateStore : IGameStateStore<DartsResolveRoundCommand, DartsQueuedState>
{
    public async Task<DartsQueuedState> LoadAsync(
        DartsResolveRoundCommand command, IGameExecutionContext context, CancellationToken ct) =>
        new(await DartsAtomicSql.ByIdAsync(command.RoundId, context, ct), 0);

    public Task SaveAsync(
        DartsResolveRoundCommand command, DartsQueuedState state, IGameExecutionContext context, CancellationToken ct) =>
        DartsAtomicSql.DeleteAsync(command.RoundId, context, ct);
}

public sealed class DartsAbortRoundStateStore : IGameStateStore<DartsAbortRoundCommand, DartsQueuedState>
{
    public async Task<DartsQueuedState> LoadAsync(
        DartsAbortRoundCommand command, IGameExecutionContext context, CancellationToken ct) =>
        new(await DartsAtomicSql.ByIdAsync(command.RoundId, context, ct), 0);

    public Task SaveAsync(
        DartsAbortRoundCommand command, DartsQueuedState state, IGameExecutionContext context, CancellationToken ct) =>
        DartsAtomicSql.DeleteAsync(command.RoundId, context, ct);
}

internal static class DartsAtomicSql
{
    private const string Select = """
        SELECT id AS Id,user_id AS UserId,chat_id AS ChatId,amount AS Amount,created_at AS CreatedAt,
               status AS Status,bot_message_id AS BotMessageId,reply_to_message_id AS ReplyToMessageId,channel AS Channel
        FROM darts_rounds
        """;

    public static async Task<DartsRound?> ByIdAsync(
        long id, IGameExecutionContext context, CancellationToken ct)
    {
        var row = await context.QuerySingleOrDefaultAsync<Row>($"{Select} WHERE id=@id FOR UPDATE", new { id }, ct);
        return row?.ToDomain();
    }

    public static async Task DeleteAsync(long id, IGameExecutionContext context, CancellationToken ct)
    {
        var deleted = await context.ExecuteAsync("DELETE FROM darts_rounds WHERE id=@id", new { id }, ct);
        if (deleted != 1) throw new InvalidOperationException("Darts round changed before commit.");
    }

    private sealed record Row(long Id, long UserId, long ChatId, int Amount, DateTime CreatedAt,
        short Status, int? BotMessageId, int ReplyToMessageId, string? Channel)
    {
        public DartsRound ToDomain() => new(Id, UserId, ChatId, Amount,
            new DateTimeOffset(DateTime.SpecifyKind(CreatedAt, DateTimeKind.Utc)),
            (DartsRoundStatus)Status, BotMessageId, ReplyToMessageId,
            Enum.TryParse<BotChannel>(Channel, ignoreCase: true, out var channel)
                ? channel : BotChannel.Telegram);
    }
}
