namespace ChatAdministration.Telegram.Infrastructure;

internal sealed record VerificationRow(
    Guid SessionId,
    long ChatId,
    long UserId,
    string Status,
    string ChallengeType,
    string CorrectAnswer,
    string OptionsJson,
    int Attempts,
    int MaximumAttempts,
    DateTime CreatedAt,
    DateTime ExpiresAt,
    int? ChallengeMessageId);
