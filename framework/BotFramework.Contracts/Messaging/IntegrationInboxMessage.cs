namespace BotFramework.Contracts.Messaging;

/// <summary>Transport-independent metadata persisted by the integration inbox.</summary>
public sealed record IntegrationInboxMessage(
    string MessageId,
    string MessageType,
    string ContractType,
    int SchemaVersion,
    string Payload,
    DateTimeOffset OccurredAt,
    string CorrelationId,
    string CausationId,
    string? TenantId,
    string? ScopeId,
    string? PlayerId,
    BotChannel Channel,
    string? Topic = null,
    string? MessageKey = null);
