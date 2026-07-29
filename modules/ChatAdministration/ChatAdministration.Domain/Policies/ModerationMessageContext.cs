using ChatAdministration.Domain.Models;

namespace ChatAdministration.Domain.Policies;

public sealed record ModerationMessageContext
{
    public required ChatState Chat { get; init; }
    public required MemberState Author { get; init; }
    public required NormalizedMessage Message { get; init; }
    public ModerationHistorySummary History { get; init; } = new();
    public RateLimitSnapshot RateLimits { get; init; } = new();
}
