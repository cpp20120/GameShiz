namespace CasinoShiz.Host.Pages.Admin;

public sealed record ChatAdministrationAuditRow(
    long Id,
    long? ActorUserId,
    long? TargetUserId,
    string Action,
    string CorrelationId,
    Guid? CaseId,
    string MetadataJson,
    DateTime CreatedAt);
