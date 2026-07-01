namespace BotFramework.Host.TelegramOutbox;

public enum TelegramOutboxRescheduleOutcome
{
    Rescheduled,
    NotFound,
    AlreadySent,
    ActivelySending,
}
