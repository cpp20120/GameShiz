using ChatAdministration.Domain.Models;

namespace ChatAdministration.Domain.Policies;

public sealed record ModerationCaseRevocationRequested(ModerationCaseState Case) : DomainEvent
{
    public string EventType => "moderation_case_revocation_requested";
}
