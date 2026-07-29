namespace ChatAdministration.Domain.Models;

public sealed record CommandSpamPolicy
{
    public bool Enabled { get; init; }
    public TimeSpan Window { get; init; } = TimeSpan.FromSeconds(30);
    public int MaximumCommands { get; init; } = 5;
    public int Score { get; init; } = 6;
}
