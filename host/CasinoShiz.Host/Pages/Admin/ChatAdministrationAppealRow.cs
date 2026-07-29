namespace CasinoShiz.Host.Pages.Admin;

public sealed record ChatAdministrationAppealRow(
    Guid AppealId,
    Guid CaseId,
    long AuthorUserId,
    string Text,
    string Status,
    long? ResolvedBy,
    string? ResolutionComment,
    DateTime CreatedAt,
    DateTime? ResolvedAt);
