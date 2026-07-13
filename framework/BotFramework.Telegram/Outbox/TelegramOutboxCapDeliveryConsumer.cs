using BotFramework.Host.Contracts.Telegram;
using BotFramework.Host.TelegramOutbox;
using DotNetCore.CAP;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace BotFramework.Telegram.Outbox;

/// <summary>Telegram-BFF endpoint of Backend → CAP → Telegram delivery.</summary>
public sealed class TelegramOutboxCapDeliveryConsumer(
    ITelegramBotClient bot,
    ICapPublisher publisher) : ICapSubscribe
{
    [CapSubscribe(TelegramOutboxCapRelayService.DeliveryTopic, Group = "casinoshiz.telegram-outbox-bff")]
    public async Task HandleAsync(TelegramOutboxDeliveryRequested request, CancellationToken ct)
    {
        var sent = await bot.SendMessage(
            request.ChatId,
            request.Text,
            parseMode: ToTelegramParseMode(request.ParseMode),
            cancellationToken: ct);
        await publisher.PublishAsync(
            TelegramOutboxCapReceiptConsumer.ConfirmationTopic,
            new TelegramOutboxDeliveryConfirmed(request.OutboxId, sent.MessageId),
            cancellationToken: ct);
    }

    private static ParseMode ToTelegramParseMode(OutboundParseMode mode) => mode switch
    {
        OutboundParseMode.Html => ParseMode.Html,
        OutboundParseMode.Markdown => ParseMode.Markdown,
        OutboundParseMode.MarkdownV2 => ParseMode.MarkdownV2,
        _ => ParseMode.None,
    };
}
