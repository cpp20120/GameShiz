using ChatAdministration.Domain.Models;

namespace ChatAdministration.Domain.Policies;

public sealed record ChatRegistered(ChatState Chat) : DomainEvent
{
    public string EventType => nameof(ChatRegistered);
}
