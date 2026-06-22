using BotFramework.Sdk;

namespace BotFramework.Host.Contracts.Rendering;

public interface IMediaRenderer<in TAggregate> where TAggregate : IAggregateRoot
{
    RenderedMedia Render(TAggregate aggregate, long viewerUserId, string cultureCode);
}
