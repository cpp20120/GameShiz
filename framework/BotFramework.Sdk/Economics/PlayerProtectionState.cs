namespace BotFramework.Sdk.Economics;

public sealed record PlayerProtectionState(
    int? DailyStakeLimit,
    DateTimeOffset? CooldownUntil,
    DateTimeOffset? SelfExcludedUntil,
    long UsedToday);
