namespace Games.Meta.Domain.Risk;

public sealed record RiskFlag(
    long Id,
    long SeasonId,
    long ChatId,
    long UserId,
    string DisplayName,
    string Kind,
    string Severity,
    string Status,
    string Reason,
    string EvidenceJson,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ResolvedAt);
