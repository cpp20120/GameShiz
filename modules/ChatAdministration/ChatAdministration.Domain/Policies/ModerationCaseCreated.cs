using ChatAdministration.Domain.Models;

namespace ChatAdministration.Domain.Policies;

public sealed record ModerationCaseCreated(ModerationCaseState Case) : DomainEvent
{
    public string EventType => "moderation_case_created";
}
