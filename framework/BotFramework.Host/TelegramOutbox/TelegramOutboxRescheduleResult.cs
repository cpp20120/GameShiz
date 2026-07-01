namespace BotFramework.Host.TelegramOutbox;

public sealed record TelegramOutboxRescheduleResult(
    TelegramOutboxRescheduleOutcome Outcome,
    string Message)
{
    public bool Success => Outcome == TelegramOutboxRescheduleOutcome.Rescheduled;
}
