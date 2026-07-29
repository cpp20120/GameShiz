namespace ChatAdministration.Domain.Models;

public sealed record MediaTypePolicy
{
    public IReadOnlySet<MessageContentType> BlockedTypes { get; init; } = new HashSet<MessageContentType>();
    public int Score { get; init; } = 6;
}
