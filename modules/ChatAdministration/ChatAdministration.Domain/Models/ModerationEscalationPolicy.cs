namespace ChatAdministration.Domain.Models;

public sealed record ModerationEscalationPolicy
{
    public int DeleteThreshold { get; init; } = 4;
    public int WarningThreshold { get; init; } = 7;
    public int MuteThreshold { get; init; } = 10;
    public int BanThreshold { get; init; } = 20;
    public TimeSpan MuteDuration { get; init; } = TimeSpan.FromMinutes(10);
    public TimeSpan? BanDuration { get; init; }
}
