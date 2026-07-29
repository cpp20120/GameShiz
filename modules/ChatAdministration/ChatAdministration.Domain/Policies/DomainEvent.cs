namespace ChatAdministration.Domain.Policies;

public interface DomainEvent
{
    string EventType { get; }
}
