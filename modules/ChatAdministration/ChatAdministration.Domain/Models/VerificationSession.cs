namespace ChatAdministration.Domain.Models;

public sealed record VerificationSession
{
    public required VerificationSessionId Id { get; init; }
    public required ChatId ChatId { get; init; }
    public required UserId UserId { get; init; }
    public VerificationStatus Status { get; init; } = VerificationStatus.Pending;
    public string ChallengeType { get; init; } = "buttons";
    public required string CorrectAnswer { get; init; }
    public IReadOnlyList<string> Options { get; init; } = [];
    public int Attempts { get; init; }
    public int MaximumAttempts { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }
    public int? ChallengeMessageId { get; init; }
}
