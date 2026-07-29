namespace ChatAdministration.Domain.Models;

public sealed record ModerationHistorySummary
{
    public IReadOnlyList<string> RecentMessageHashes { get; init; } = [];
    public int ViolationsInWindow { get; init; }
}
