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
