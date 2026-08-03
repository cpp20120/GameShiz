namespace BotFramework.Contracts.Messaging;

/// <summary>Stable transport envelope for an addressed integration command.</summary>
public sealed record IntegrationCommandEnvelope(
    string MessageId,
    string CommandType,
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
