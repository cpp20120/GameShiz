using BotFramework.Host;
using Dapper;

namespace Games.Basketball;

public sealed record BasketballBet(long UserId, long ChatId, int Amount, DateTimeOffset CreatedAt);
