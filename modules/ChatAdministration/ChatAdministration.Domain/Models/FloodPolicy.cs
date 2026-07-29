namespace ChatAdministration.Domain.Models;

public sealed record FloodPolicy
{
    public TimeSpan Window { get; init; } = TimeSpan.FromSeconds(10);
    public int MaximumMessages { get; init; } = 6;
    public bool DeleteMessages { get; init; } = true;
    public TimeSpan MuteDuration { get; init; } = TimeSpan.FromMinutes(10);
}
