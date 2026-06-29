namespace BotFramework.Host.Contracts.ResponsibleGaming;

public sealed record PlayerStats(
    long UserId,
    long BalanceScopeId,
    int Balance,
    long Stake7Days,
    long Payout7Days,
    long Stake30Days,
    long Payout30Days,
    long StakeToday,
    int? DailyStakeLimit,
    DateTimeOffset? CooldownUntil,
    DateTimeOffset? SelfExcludedUntil);
