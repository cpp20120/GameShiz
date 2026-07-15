namespace BotFramework.Host.Contracts.Economics;

public interface IDailyBonusService
{
    Task<DailyBonusClaimResult> TryClaimAsync(
        long userId, long balanceScopeId, string displayName, CancellationToken ct);

    Task<DailyBonusCatchUpStats> CatchUpMissedDaysAsync(CancellationToken ct);
}
