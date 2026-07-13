using DotNetCore.CAP;

namespace BotFramework.Host.TelegramOutbox;

/// <summary>Completes the database outbox only after the BFF confirms Telegram delivery.</summary>
public sealed class TelegramOutboxCapReceiptConsumer(ITelegramOutboxStore store) : ICapSubscribe
{
    public const string ConfirmationTopic = "telegram.outbox.delivery-confirmed";

    [CapSubscribe(ConfirmationTopic, Group = "casinoshiz.telegram-outbox-backend")]
    public Task HandleAsync(TelegramOutboxDeliveryConfirmed confirmation, CancellationToken ct) =>
        store.MarkSentAsync(confirmation.OutboxId, confirmation.TelegramMessageId, ct);
}
