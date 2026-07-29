namespace CasinoShiz.Host.Pages.Admin;

public sealed record ChatAdministrationCaseRow(
    Guid CaseId,
    long TargetUserId,
    long? ActorUserId,
    string ActorType,
    string Action,
    string? Reason,
    string Status,
    DateTime CreatedAt,
    DateTime? ExpiresAt,
    string? SourceRuleId);
