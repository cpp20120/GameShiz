namespace CasinoShiz.Host.Pages.Admin;

public sealed record ChatAdministrationVerificationRow(
    Guid SessionId,
    long UserId,
    string Status,
    string ChallengeType,
    int Attempts,
    int MaximumAttempts,
    DateTime CreatedAt,
    DateTime ExpiresAt,
    int? ChallengeMessageId);
