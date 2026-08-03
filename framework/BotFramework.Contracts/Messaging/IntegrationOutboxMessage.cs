namespace BotFramework.Contracts.Messaging;

/// <summary>
/// Durable outgoing integration record. Its envelope is retained so the
/// relay can publish without re-running domain code.
/// </summary>
public sealed record IntegrationOutboxMessage(
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
    BotChannel Channel);
