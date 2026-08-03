namespace BotFramework.Contracts.Messaging;

/// <summary>
/// Stable transport envelope for framework-owned integration events.
/// The payload stays contract-specific; routing, tracing, tenant scope and
/// idempotency metadata stay outside individual event records.
/// </summary>
public sealed record IntegrationEventEnvelope(
    string MessageId,
    string EventType,
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
