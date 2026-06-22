namespace Games.Meta;

public sealed record RiskFlagView(
    long Id,
    long ChatId,
    long UserId,
    string DisplayName,
    string Kind,
    string Severity,
    string Status,
    string Reason,
    DateTimeOffset CreatedAt);
