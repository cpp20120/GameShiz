using BotFramework.Sdk;
using Microsoft.Extensions.DependencyInjection;

namespace BotFramework.Host.Events.Stores;

public sealed class DefaultAggregateFactory<TAggregate>(IServiceProvider services) : IAggregateFactory<TAggregate>
    where TAggregate : class, IAggregateRoot
{
    public TAggregate Create(string id)
    {
        try
        {
            return ActivatorUtilities.CreateInstance<TAggregate>(services, id);
        }
        catch (InvalidOperationException)
        {
            try
            {
                return ActivatorUtilities.CreateInstance<TAggregate>(services);
            }
            catch (InvalidOperationException ex)
            {
                throw new InvalidOperationException(
                    $"Cannot create aggregate {typeof(TAggregate).FullName}. " +
                    "Register a custom IAggregateFactory<TAggregate>, or expose a public constructor " +
                    "that can be satisfied by DI, optionally with the stream id as a string argument.",
                    ex);
            }
        }
    }
}
