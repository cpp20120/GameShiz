namespace Games.Blackjack.Application.Services;

public interface IBlackjackService
{
    Task<BlackjackResult> StartAsync(long userId, string displayName, long chatId, int bet, string operationId, CancellationToken ct);
    Task<BlackjackResult> HitAsync(long userId, CancellationToken ct);
    Task<BlackjackResult> StandAsync(long userId, CancellationToken ct);
    Task<BlackjackResult> DoubleAsync(long userId, CancellationToken ct);
    Task<(BlackjackSnapshot? snapshot, int? stateMessageId)> GetSnapshotAsync(long userId, CancellationToken ct);
    Task SetStateMessageIdAsync(long userId, int messageId, CancellationToken ct);
}
