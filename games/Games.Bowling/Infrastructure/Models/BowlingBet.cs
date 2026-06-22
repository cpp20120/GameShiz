using BotFramework.Host;
using Dapper;

namespace Games.Bowling;

public sealed record BowlingBet(long UserId, long ChatId, int Amount, DateTimeOffset CreatedAt);
