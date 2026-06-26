namespace BotFramework.Host.Contracts.Telegram;

public interface ITelegramOutbox
{
    Task EnqueueAsync(TelegramOutboxMessage message, CancellationToken ct);
}
