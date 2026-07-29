namespace ChatAdministration.Domain.Models;

public sealed record ForwardedMessagePolicy
{
    public bool Enabled { get; init; }
    public int Score { get; init; } = 6;
}
