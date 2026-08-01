using ChatAdministration.Domain.Models;

namespace ChatAdministration.Domain.Policies;

public sealed record WarningRevoked(
    ChatId ChatId,
    UserId TargetUserId,
    WarningId WarningId,
    WarningRevocationReason Reason) : IDomainEvent
{
    public string EventType => "warning_revoked";
}
