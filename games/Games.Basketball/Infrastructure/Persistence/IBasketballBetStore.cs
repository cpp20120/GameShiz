using BotFramework.Host;
using Dapper;

namespace Games.Basketball.Infrastructure.Persistence;

public interface IBasketballBetStore
{
    Task<BasketballBet?> FindAsync(long userId, long chatId, CancellationToken ct);
    Task<bool> InsertAsync(BasketballBet bet, CancellationToken ct);
    Task DeleteAsync(long userId, long chatId, CancellationToken ct);
}
