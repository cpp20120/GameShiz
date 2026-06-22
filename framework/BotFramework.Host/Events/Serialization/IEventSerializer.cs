using BotFramework.Sdk;

namespace BotFramework.Host.Events;

public interface IEventSerializer
{
    string Serialize(IDomainEvent ev);
    IDomainEvent? Deserialize(string eventType, string payloadJson);
}
