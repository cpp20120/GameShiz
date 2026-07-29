namespace ChatAdministration.Domain.Models;

public sealed record AppealState
{
    public required AppealId Id { get; init; }
    public required ModerationCaseId CaseId { get; init; }
    public required UserId AuthorUserId { get; init; }
    public required string Text { get; init; }
    public AppealStatus Status { get; init; } = AppealStatus.Open;
    public UserId? ResolvedBy { get; init; }
    public string? ResolutionComment { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? ResolvedAt { get; init; }
}
