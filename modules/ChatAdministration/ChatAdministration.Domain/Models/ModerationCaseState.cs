namespace ChatAdministration.Domain.Models;

public sealed record ModerationCaseState
{
    public required ModerationCaseId Id { get; init; }
    public required ChatId ChatId { get; init; }
    public required UserId TargetUserId { get; init; }
    public UserId? ActorUserId { get; init; }
    public ModerationActorType ActorType { get; init; } = ModerationActorType.Human;
    public ModerationAction Action { get; init; }
    public string? Reason { get; init; }
    public int? SourceMessageId { get; init; }
    public RuleId? SourceRuleId { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? ExpiresAt { get; init; }
    public ModerationCaseStatus Status { get; init; } = ModerationCaseStatus.Requested;
    public required string CorrelationId { get; init; }
}
