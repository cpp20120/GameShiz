using Games.Blackjack.Domain.Results;

namespace Games.Blackjack.Contracts;

public sealed record BlackjackState(BlackjackSnapshot? Snapshot, int? StateMessageId);

public interface IBlackjackClient
{
    Task<BlackjackResult> StartAsync(long userId, string displayName, long chatId,
        int bet, string operationId, CancellationToken ct);
    Task<BlackjackResult> HitAsync(long userId, CancellationToken ct);
    Task<BlackjackResult> StandAsync(long userId, CancellationToken ct);
    Task<BlackjackResult> DoubleAsync(long userId, CancellationToken ct);
    Task<BlackjackState> GetStateAsync(long userId, CancellationToken ct);
    Task SetStateMessageIdAsync(long userId, int messageId, CancellationToken ct);
}
