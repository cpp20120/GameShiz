using ChatAdministration.Domain.Models;

namespace ChatAdministration.Domain.Policies;

public sealed record RoleDecision(
    bool Accepted,
    string? ErrorCode,
    MemberState? Member,
    IReadOnlyList<DomainEvent> Events)
{
    public static RoleDecision Reject(string errorCode) => new(false, errorCode, null, []);
}
