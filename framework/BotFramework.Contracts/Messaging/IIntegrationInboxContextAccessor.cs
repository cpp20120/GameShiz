namespace BotFramework.Contracts.Messaging;

/// <summary>
/// Provides the current inbox transaction to handlers resolved by the
/// framework dispatcher. Handlers that need atomic domain writes should use
/// <see cref="IIntegrationInbox.ExecuteOnceAsync{TResult}"/> directly or this
/// accessor from a dispatcher callback.
/// </summary>
public interface IIntegrationInboxContextAccessor
{
    IntegrationInboxContext? Current { get; }

    IntegrationInboxContext RequireCurrent() =>
        Current ?? throw new InvalidOperationException("Integration inbox context is unavailable.");

    IDisposable Push(IntegrationInboxContext context);
}
