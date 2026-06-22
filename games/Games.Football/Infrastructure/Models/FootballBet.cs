using BotFramework.Host;
using Dapper;

namespace Games.Football;

public sealed record FootballBet(long UserId, long ChatId, int Amount, DateTimeOffset CreatedAt);
