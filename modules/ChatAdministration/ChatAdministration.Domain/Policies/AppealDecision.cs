using ChatAdministration.Domain.Models;

namespace ChatAdministration.Domain.Policies;

public sealed record AppealDecision(
    bool Accepted,
    string? ErrorCode,
    AppealState? Appeal,
    IReadOnlyList<IDomainEvent> Events)
{
    public static AppealDecision Reject(string errorCode) => new(false, errorCode, null, []);
}
