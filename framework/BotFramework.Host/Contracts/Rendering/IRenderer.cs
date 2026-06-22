using BotFramework.Sdk;

namespace BotFramework.Host.Contracts.Rendering;

public interface IRenderer<in TAggregate> where TAggregate : IAggregateRoot
{
    RenderedMessage Render(TAggregate aggregate, long viewerUserId, string cultureCode);
}
