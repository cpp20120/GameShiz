namespace BotFramework.Host.Contracts.ResponsibleGaming;

public sealed class PlayerProtectionException : Exception
{
    public PlayerProtectionException()
        : this("player_protection")
    {
    }

    public PlayerProtectionException(string reasonCode)
        : this(reasonCode, null, null, null)
    {
    }

    public PlayerProtectionException(string reasonCode, Exception innerException)
        : base(reasonCode, innerException)
    {
        ReasonCode = reasonCode;
    }

    public PlayerProtectionException(string reasonCode, DateTimeOffset? blockedUntil)
        : this(reasonCode, blockedUntil, null, null)
    {
    }

    public PlayerProtectionException(string reasonCode, DateTimeOffset? blockedUntil, int? dailyLimit)
        : this(reasonCode, blockedUntil, dailyLimit, null)
    {
    }

    public PlayerProtectionException(string reasonCode, DateTimeOffset? blockedUntil, int? dailyLimit, long? usedToday)
        : base(reasonCode)
    {
        ReasonCode = reasonCode;
        BlockedUntil = blockedUntil;
        DailyLimit = dailyLimit;
        UsedToday = usedToday;
    }

    public PlayerProtectionException(string reasonCode, int? dailyLimit, long? usedToday)
        : this(reasonCode, null, dailyLimit, usedToday)
    {
    }

    public string ReasonCode { get; }
    public DateTimeOffset? BlockedUntil { get; }
    public int? DailyLimit { get; }
    public long? UsedToday { get; }
}
