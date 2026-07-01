namespace BotFramework.Host.TelegramOutbox;

public static class TelegramOutboxReschedulePolicy
{
    public static TelegramOutboxRescheduleOutcome Classify(
        string status,
        DateTimeOffset? lockedUntil,
        DateTimeOffset now)
    {
        if (string.Equals(status, "sent", StringComparison.Ordinal))
            return TelegramOutboxRescheduleOutcome.AlreadySent;

        if (string.Equals(status, "sending", StringComparison.Ordinal) && lockedUntil > now)
            return TelegramOutboxRescheduleOutcome.ActivelySending;

        return status is "pending" or "sending"
            ? TelegramOutboxRescheduleOutcome.Rescheduled
            : TelegramOutboxRescheduleOutcome.AlreadySent;
    }
}
