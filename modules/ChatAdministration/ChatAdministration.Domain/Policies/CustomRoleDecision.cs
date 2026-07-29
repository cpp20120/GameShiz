using ChatAdministration.Domain.Models;

namespace ChatAdministration.Domain.Policies;

public sealed record CustomRoleDecision(
    bool Accepted,
    string? ErrorCode,
    ChatSettings? Settings)
{
    public static CustomRoleDecision Reject(string errorCode) => new(false, errorCode, null);
}
