
using Telegram.Bot;

namespace BotFramework.Host.Pipeline.Middleware;

public sealed partial class ExceptionMiddleware(
    IAnalyticsService analytics,
    ILogger<ExceptionMiddleware> logger) : IUpdateMiddleware
{
    public async Task InvokeAsync(UpdateContext ctx, UpdateDelegate next)
    {
        try
        {
            await next(ctx);
        }
        catch (OperationCanceledException) when (ctx.Ct.IsCancellationRequested)
        {
            throw;
        }
        catch (PlayerProtectionException ex)
        {
            var chatId = ctx.Update.Message?.Chat.Id ?? ctx.Update.CallbackQuery?.Message?.Chat.Id;
            if (chatId is null) return;

            var text = ex.ReasonCode switch
            {
                "self_excluded" => $"🛡 Самоисключение активно до <code>{ex.BlockedUntil:yyyy-MM-dd HH:mm} UTC</code>.",
                "cooldown" => $"⏸ Перерыв в игре активен до <code>{ex.BlockedUntil:yyyy-MM-dd HH:mm} UTC</code>.",
                "daily_limit" => $"🛡 Дневной лимит ставок достигнут: <b>{ex.UsedToday}/{ex.DailyLimit}</b> монет (UTC).",
                _ => "🛡 Ставка заблокирована настройками ответственной игры.",
            };
            analytics.Track("responsible_gaming", "wager_blocked", new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["user_id"] = ctx.UserId,
                ["reason"] = ex.ReasonCode,
                ["daily_limit"] = ex.DailyLimit,
                ["used_today"] = ex.UsedToday,
                ["blocked_until"] = ex.BlockedUntil,
            });
            await ctx.Bot.SendMessage(chatId.Value, text,
                parseMode: global::Telegram.Bot.Types.Enums.ParseMode.Html,
                cancellationToken: ctx.Ct);
        }
        catch (Exception ex)
        {
            LogUpdateError(ctx.Update.Id, ctx.UserId, ex);
            analytics.Track("_framework", "error", new Dictionary<string, object?>
(StringComparer.Ordinal)
            {
                ["exception_type"] = ex.GetType().FullName,
                ["message"] = ex.Message,
                ["stack"] = ex.StackTrace,
                ["update_id"] = ctx.Update.Id,
                ["user_id"] = ctx.UserId,
            });
            throw;
        }
    }

    [LoggerMessage(EventId = 1900, Level = LogLevel.Error,
        Message = "update.error update_id={UpdateId} user={UserId}")]
    partial void LogUpdateError(int updateId, long userId, Exception exception);
}
