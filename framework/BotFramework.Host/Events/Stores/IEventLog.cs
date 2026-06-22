using BotFramework.Sdk;

namespace BotFramework.Host.Events.Stores;

public interface IEventLog
{
    Task AppendAsync(IDomainEvent ev, CancellationToken ct);
}
