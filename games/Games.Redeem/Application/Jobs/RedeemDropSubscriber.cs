using System.Globalization;
using BotFramework.Contracts.Messaging;
using BotFramework.Host.Contracts.Telegram;
using Microsoft.Extensions.DependencyInjection;

namespace Games.Redeem.Application.Jobs;

public sealed partial class RedeemDropSubscriber(
    IServiceProvider services,
    ITelegramOutbox telegramOutbox,
    ILocalizer localizer,
    ILogger<RedeemDropSubscriber> logger) : IDomainEventSubscriber
{
    private const string DropEventType = "minigame.redeem_code_drop_requested";

    public async Task HandleAsync(IDomainEvent ev, CancellationToken ct)
    {
        if (!string.Equals(ev.EventType, DropEventType, StringComparison.Ordinal)) return;
        if (ev is not MiniGameRedeemCodeDropRequested drop
            || drop.Channel != BotChannel.Telegram
            || !TryReadDrop(drop, out var userId, out var chatId, out var gameId)) return;

        try
        {
            await using var scope = services.CreateAsyncScope();
            var redeem = scope.ServiceProvider.GetRequiredService<IRedeemService>();

            var code = await redeem.IssueAdminCodeAsync(userId: 0, ct, gameId);
            await telegramOutbox.EnqueueAsync(
                new TelegramOutboxMessage(
                    chatId,
                    string.Format(CultureInfo.InvariantCulture, localizer.Get("redeem", "drop.message"), code),
                    DedupeKey: $"redeem-drop:{userId}:{chatId}:{gameId}:{ev.OccurredAt}",
                    ParseMode: OutboundParseMode.Html),
                ct);
        }
        catch (Exception ex)
        {
            LogDropFailed(ex);
            throw;
        }
    }

    private static bool TryReadDrop(IDomainEvent ev, out long userId, out long chatId, out string gameId)
    {
        var type = ev.GetType();
        userId = ReadLong(type, ev, "UserId");
        chatId = ReadLong(type, ev, "ChatId");
        gameId = ReadString(type, ev, "GameId");
        return userId != 0 && chatId != 0 && !string.IsNullOrWhiteSpace(gameId);
    }

    private static long ReadLong(Type type, object instance, string propertyName) =>
        type.GetProperty(propertyName)?.GetValue(instance) switch
        {
            long value => value,
            int value => value,
            _ => 0,
        };

    private static string ReadString(Type type, object instance, string propertyName) =>
        type.GetProperty(propertyName)?.GetValue(instance) as string ?? "";

    [LoggerMessage(LogLevel.Warning, "redeem.telegram_drop_failed")]
    partial void LogDropFailed(Exception ex);
}
