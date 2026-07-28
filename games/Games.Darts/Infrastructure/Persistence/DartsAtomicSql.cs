using BotFramework.Contracts.Messaging;
using BotFramework.Host.Execution;
using Games.Darts.Application.Execution;

namespace Games.Darts.Infrastructure.Persistence;

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
