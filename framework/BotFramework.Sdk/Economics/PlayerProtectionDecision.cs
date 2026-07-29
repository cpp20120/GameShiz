namespace BotFramework.Sdk.Economics;

public sealed record PlayerProtectionDecision(
    bool Allowed,
    string? ReasonCode,
    DateTimeOffset? BlockedUntil,
    int? DailyStakeLimit,
    long UsedToday);
