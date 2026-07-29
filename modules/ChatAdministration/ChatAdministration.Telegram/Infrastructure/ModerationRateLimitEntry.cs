namespace ChatAdministration.Telegram.Infrastructure;

internal sealed record ModerationRateLimitEntry(
    DateTimeOffset SentAt,
    string Hash,
    bool HasLink,
    int CommandCount);
