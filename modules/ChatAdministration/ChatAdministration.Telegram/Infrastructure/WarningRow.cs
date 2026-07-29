namespace ChatAdministration.Telegram.Infrastructure;

internal sealed record WarningRow(
    Guid WarningId,
    long ChatId,
    long TargetUserId,
    long? ActorUserId,
    string? Reason,
    DateTime CreatedAt,
    DateTime? ExpiresAt,
    bool IsActive,
    string? RevocationReason);
