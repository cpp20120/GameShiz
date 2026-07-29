using ChatAdministration.Domain.Models;

namespace ChatAdministration.Domain.Policies;

public sealed record WarningDecision(
    bool Accepted,
    string? ErrorCode,
    WarningState? Warning,
    IReadOnlyList<DomainEvent> Events)
{
    public static WarningDecision Reject(string errorCode) => new(false, errorCode, null, []);
}
