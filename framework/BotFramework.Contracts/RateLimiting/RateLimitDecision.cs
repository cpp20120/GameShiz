namespace BotFramework.Contracts.RateLimiting;

public sealed record RateLimitDecision(
    bool Allowed,
    RateLimitDimension? DeniedDimension,
    int Limit,
    int Remaining,
    TimeSpan RetryAfter,
    bool IsFallback,
    string PolicyVersion);
