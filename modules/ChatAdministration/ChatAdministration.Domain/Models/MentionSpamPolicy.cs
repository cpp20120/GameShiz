namespace ChatAdministration.Domain.Models;

public sealed record MentionSpamPolicy
{
    public bool Enabled { get; init; }
    public int MaximumMentions { get; init; } = 5;
}
