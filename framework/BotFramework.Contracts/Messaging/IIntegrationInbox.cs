namespace BotFramework.Contracts.Messaging;

/// <summary>
/// Durable, tenant-scoped idempotency boundary for integration message
/// handlers. The callback and the inbox marker are committed together when
/// the callback uses the supplied database transaction.
/// </summary>
public interface IIntegrationInbox
{
    Task<IntegrationInboxResult<TResult>> ExecuteOnceAsync<TResult>(
        IntegrationInboxMessage message,
        Func<IntegrationInboxContext, CancellationToken, Task<TResult>> execute,
        CancellationToken ct);

    Task<IntegrationInboxResult> ExecuteOnceAsync(
        IntegrationInboxMessage message,
        Func<IntegrationInboxContext, CancellationToken, Task> execute,
        CancellationToken ct);
}
