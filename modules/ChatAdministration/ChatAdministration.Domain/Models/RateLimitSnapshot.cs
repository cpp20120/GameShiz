namespace ChatAdministration.Domain.Models;

public sealed record RateLimitSnapshot
{
    public int MessagesInWindow { get; init; }
    public int LinksInWindow { get; init; }
    public int CommandsInWindow { get; init; }
}
