using BotFramework.Host;
using BotFramework.Sdk;
using Dapper;

namespace Games.Blackjack.Infrastructure.Persistence;

public interface IBlackjackHandStore
{
    Task<BlackjackHandRow?> FindAsync(long userId, CancellationToken ct);
    Task<bool> InsertAsync(BlackjackHandRow hand, CancellationToken ct);
    Task UpdateAsync(BlackjackHandRow hand, CancellationToken ct);
    Task DeleteAsync(long userId, CancellationToken ct);
    Task<IReadOnlyList<long>> ListStuckUserIdsAsync(DateTimeOffset cutoff, CancellationToken ct);
    Task SetStateMessageIdAsync(long userId, int messageId, CancellationToken ct);
}
