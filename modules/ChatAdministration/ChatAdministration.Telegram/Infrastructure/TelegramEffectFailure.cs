namespace ChatAdministration.Telegram.Infrastructure;

public sealed record TelegramEffectFailure(
    TelegramEffectOutcome Outcome,
    string Code,
    string Message,
    TimeSpan? RetryAfter = null);
