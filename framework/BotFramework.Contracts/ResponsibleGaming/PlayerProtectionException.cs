namespace BotFramework.Host.Contracts.ResponsibleGaming;

public sealed class PlayerProtectionException(
    string reasonCode,
    DateTimeOffset? blockedUntil = null,
    int? dailyLimit = null,
    long? usedToday = null) : Exception(reasonCode)
{
    public string ReasonCode { get; } = reasonCode;
    public DateTimeOffset? BlockedUntil { get; } = blockedUntil;
    public int? DailyLimit { get; } = dailyLimit;
    public long? UsedToday { get; } = usedToday;
}
