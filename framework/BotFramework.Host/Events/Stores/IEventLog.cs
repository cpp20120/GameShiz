using BotFramework.Sdk;

namespace BotFramework.Host.Events;

public interface IEventLog
{
    Task AppendAsync(IDomainEvent ev, CancellationToken ct);
}
