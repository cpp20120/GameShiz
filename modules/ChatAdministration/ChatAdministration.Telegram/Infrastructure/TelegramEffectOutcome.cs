namespace ChatAdministration.Telegram.Infrastructure;

public enum TelegramEffectOutcome
{
    Applied,
    AlreadyApplied,
    Retryable,
    Permanent,
    Unknown,
}
