using BotFramework.Contracts.Tenancy;

namespace BotFramework.Rest;

public sealed record RestRequestContext(
    string Subject,
    long UserId,
    string DisplayName,
    string ScopeId,
    string CorrelationId,
    string? IdempotencyKey,
    IReadOnlyDictionary<string, string> Baggage)
{
    /// <summary>Opaque tenant identity from the canonical REST route.</summary>
    public TenantId Tenant { get; init; }

    /// <summary>Opaque scope identity from the canonical REST route.</summary>
    public ScopeId Scope { get; init; }

    /// <summary>Opaque player identity from the JWT subject.</summary>
    public PlayerId Player { get; init; }

    public RequestId RequestIdentifier { get; init; }

    public RequestId CorrelationIdentifier { get; init; }

    public TenantContext TenantContext { get; init; } = null!;

    public string RequestId => IdempotencyKey ?? CorrelationId;

    public string RequireIdempotencyKey()
    {
        return string.IsNullOrWhiteSpace(IdempotencyKey) ? throw new RestBadRequestException("The Idempotency-Key header is required for state-changing requests.") : IdempotencyKey;
    }
}