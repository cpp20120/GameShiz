using ChatAdministration.Domain.Models;

namespace ChatAdministration.Domain.Effects;

public sealed record EffectEnvelope
{
    public required EffectId Id { get; init; }
    public required string EffectType { get; init; }
    public required IModerationEffect Payload { get; init; }
    public required string CorrelationId { get; init; }
    public required string CausationId { get; init; }
    public required string IdempotencyKey { get; init; }
    public EffectExecutionStatus Status { get; init; } = EffectExecutionStatus.Pending;
    public int Attempt { get; init; }
    public int MaximumAttempts { get; init; } = 8;
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? NotBefore { get; init; }
    public IReadOnlyCollection<EffectId> Dependencies { get; init; } = [];
}
