namespace BotFramework.Host;

public interface ITelegramDiceDailyRollLimiter
{
    Task<TelegramDiceRollGateResult> TryConsumeRollAsync(
        long userId, long balanceScopeId, string gameId, CancellationToken ct);

    Task<TelegramDiceRollGateResult> GetRollStatusAsync(
        long userId, long balanceScopeId, string gameId, CancellationToken ct);

    Task GrantExtraRollAsync(long userId, long balanceScopeId, string gameId, CancellationToken ct);

    Task TryRefundRollAsync(long userId, long balanceScopeId, string gameId, CancellationToken ct);
}
