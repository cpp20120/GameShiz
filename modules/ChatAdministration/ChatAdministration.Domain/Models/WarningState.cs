namespace ChatAdministration.Domain.Models;

public sealed record WarningState
{
    public required WarningId Id { get; init; }
    public required ChatId ChatId { get; init; }
    public required UserId TargetUserId { get; init; }
    public UserId? ActorUserId { get; init; }
    public string? Reason { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? ExpiresAt { get; init; }
    public bool IsActive { get; init; } = true;
    public WarningRevocationReason? RevocationReason { get; init; }
}
