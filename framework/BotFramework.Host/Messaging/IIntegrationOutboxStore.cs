using BotFramework.Contracts.Messaging;

namespace BotFramework.Host.Messaging;

public interface IIntegrationOutboxStore : IIntegrationOutbox
{
    Task<IReadOnlyList<IntegrationOutboxDelivery>> ClaimAsync(
        string producerName,
        int limit,
        TimeSpan lease,
        string leaseOwner,
        CancellationToken ct);

    Task MarkPublishedAsync(string producerName, string messageId, string leaseOwner, CancellationToken ct);

    Task MarkFailedAsync(
        string producerName,
        string messageId,
        string leaseOwner,
        string error,
        CancellationToken ct);

    Task<long> CountPendingAsync(string producerName, CancellationToken ct);
}

public sealed record IntegrationOutboxDelivery(
    long OutboxId,
    string ProducerName,
    string MessageId,
    IntegrationMessageKind Kind,
    string Topic,
    string MessageKey,
    string MessageType,
    string ContractType,
    int SchemaVersion,
    string Payload,
    string EnvelopeJson,
    DateTimeOffset OccurredAt,
    string CorrelationId,
    string CausationId,
    string? TenantId,
    string? ScopeId,
    string? PlayerId,
    BotChannel Channel,
    string LeaseOwner,
    int Attempts,
    DateTimeOffset CreatedAt);
