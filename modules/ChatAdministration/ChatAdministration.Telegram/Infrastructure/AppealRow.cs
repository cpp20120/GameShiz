namespace ChatAdministration.Telegram.Infrastructure;

internal sealed record AppealRow(
    Guid AppealId,
    Guid CaseId,
    long ChatId,
    long AuthorUserId,
    string Text,
    string Status,
    long? ResolvedBy,
    string? ResolutionComment,
    DateTime CreatedAt,
    DateTime? ResolvedAt);
