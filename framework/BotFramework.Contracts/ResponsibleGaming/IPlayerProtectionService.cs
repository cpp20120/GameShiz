namespace BotFramework.Host.Contracts.ResponsibleGaming;

public interface IPlayerProtectionService
{
    Task<PlayerStats> GetStatsAsync(long userId, long balanceScopeId, CancellationToken ct);
    Task SetDailyLimitAsync(long userId, int? limit, CancellationToken ct);
    Task SetCooldownAsync(long userId, DateTimeOffset until, CancellationToken ct);
    Task SetSelfExclusionAsync(long userId, DateTimeOffset until, CancellationToken ct);
}
