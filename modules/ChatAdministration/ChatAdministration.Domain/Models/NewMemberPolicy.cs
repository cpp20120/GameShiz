namespace ChatAdministration.Domain.Models;

public sealed record NewMemberPolicy
{
    public bool Enabled { get; init; }
    public TimeSpan Window { get; init; } = TimeSpan.FromMinutes(10);
    public int Score { get; init; } = 3;
}
