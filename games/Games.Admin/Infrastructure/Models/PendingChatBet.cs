using Dapper;

namespace Games.Admin.Infrastructure.Models;

public sealed class PendingChatBet
{
    public string GameId { get; init; } = "";
    public long UserId { get; init; }
    public long ChatId { get; init; }
    public int Amount { get; init; }
    public int? BotMessageId { get; init; }
}
