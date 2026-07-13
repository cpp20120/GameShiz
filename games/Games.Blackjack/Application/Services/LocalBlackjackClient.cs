using Games.Blackjack.Contracts;

namespace Games.Blackjack.Application.Services;

public sealed class LocalBlackjackClient(IBlackjackService service) : IBlackjackClient
{
    public Task<BlackjackResult> StartAsync(long userId, string displayName, long chatId, int bet, string operationId, CancellationToken ct) => service.StartAsync(userId, displayName, chatId, bet, operationId, ct);
    public Task<BlackjackResult> HitAsync(long userId, CancellationToken ct) => service.HitAsync(userId, ct);
    public Task<BlackjackResult> StandAsync(long userId, CancellationToken ct) => service.StandAsync(userId, ct);
    public Task<BlackjackResult> DoubleAsync(long userId, CancellationToken ct) => service.DoubleAsync(userId, ct);
    public async Task<BlackjackState> GetStateAsync(long userId, CancellationToken ct)
    {
        var (snapshot, messageId) = await service.GetSnapshotAsync(userId, ct);
        return new BlackjackState(snapshot, messageId);
    }
    public Task SetStateMessageIdAsync(long userId, int messageId, CancellationToken ct) => service.SetStateMessageIdAsync(userId, messageId, ct);
}
