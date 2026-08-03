namespace BotFramework.Contracts.Messaging;

/// <summary>
/// Transaction-aware integration outbox. Passing a transaction makes the
/// business mutation and outgoing message one local commit.
/// </summary>
public interface IIntegrationOutbox
{
    Task EnqueueAsync(
        IntegrationOutboxMessage message,
        IntegrationTransactionContext? transaction,
        CancellationToken ct);
}
