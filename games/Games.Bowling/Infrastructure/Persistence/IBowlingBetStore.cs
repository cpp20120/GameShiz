using Dapper;

namespace Games.Bowling.Infrastructure.Persistence;

public interface IBowlingBetStore
{
    Task<BowlingBet?> FindAsync(long userId, long chatId, CancellationToken ct);
    Task<bool> InsertAsync(BowlingBet bet, CancellationToken ct);
    Task DeleteAsync(long userId, long chatId, CancellationToken ct);
}
