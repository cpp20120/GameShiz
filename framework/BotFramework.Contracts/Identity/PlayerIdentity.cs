namespace BotFramework.Contracts.Identity;

public sealed record PlayerIdentity(
    long UserId,
    string DisplayName,
    string? Username,
    DateTimeOffset FirstSeenAt,
    DateTimeOffset LastSeenAt);
