namespace CasinoShiz.Host.Pages.Admin;

public sealed record ChatAdministrationWarningRow(
    Guid WarningId,
    long TargetUserId,
    long? ActorUserId,
    string? Reason,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? ExpiresAt,
    string? RevocationReason);
