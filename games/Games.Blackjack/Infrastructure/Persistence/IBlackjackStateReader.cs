using Games.Blackjack.Application.Execution;

namespace Games.Blackjack.Infrastructure.Persistence;

public interface IBlackjackStateReader
{
    Task<BlackjackGameState?> LoadAsync(long userId, CancellationToken ct);
}
