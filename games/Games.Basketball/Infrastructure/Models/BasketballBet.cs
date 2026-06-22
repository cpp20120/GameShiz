using BotFramework.Host;
using Dapper;

namespace Games.Basketball.Infrastructure.Models;

public sealed record BasketballBet(long UserId, long ChatId, int Amount, DateTimeOffset CreatedAt);
