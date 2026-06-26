using Dapper;

namespace Games.Football.Infrastructure.Models;

public sealed record FootballBet(long UserId, long ChatId, int Amount, DateTimeOffset CreatedAt);
