using BotFramework.Host;
using Dapper;

namespace Games.Bowling.Infrastructure.Models;

public sealed record BowlingBet(long UserId, long ChatId, int Amount, DateTimeOffset CreatedAt);
