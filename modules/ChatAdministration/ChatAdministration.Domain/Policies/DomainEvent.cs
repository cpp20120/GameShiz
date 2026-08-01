namespace ChatAdministration.Domain.Policies;

public interface IDomainEvent
{
    string EventType { get; }
}
