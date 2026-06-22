using BotFramework.Host;
using Dapper;

namespace Games.Football.Infrastructure.Persistence;

public interface IFootballBetStore
{
    Task<FootballBet?> FindAsync(long userId, long chatId, CancellationToken ct);
    Task<bool> InsertAsync(FootballBet bet, CancellationToken ct);
    Task DeleteAsync(long userId, long chatId, CancellationToken ct);
}
