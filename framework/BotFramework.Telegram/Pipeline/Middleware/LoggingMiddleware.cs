using System.Diagnostics;

namespace BotFramework.Telegram.Pipeline.Middleware;

public sealed partial class LoggingMiddleware(ILogger<LoggingMiddleware> logger) : IUpdateMiddleware
{
    public async Task InvokeAsync(UpdateContext ctx, UpdateDelegate next)
    {
        var update = ctx.Update;
        var kind = update switch
        {
            { Message.Text: not null }   => "text",
            { Message.Dice: not null }   => "dice",
            { CallbackQuery: not null }  => "callback",
            { ChannelPost: not null }    => "channel_post",
            { EditedMessage: not null }  => "edited_message",
            { InlineQuery: not null }    => "inline_query",
            _                       => "other",
        };

        using var scope = logger.BeginScope(new Dictionary<string, object>
(StringComparer.Ordinal)
        {
            ["update_id"] = update.Id,
            ["user_id"]   = ctx.UserId,
            ["chat_id"]   = ctx.ChatId,
            ["kind"]      = kind,
        });

        var started = Stopwatch.GetTimestamp();
        if (logger.IsEnabled(LogLevel.Debug))
        {
            var text = Truncate(ctx.Text);
            var callbackData = Truncate(ctx.CallbackData);
            LogUpdateIn(kind, ctx.UserId, text, callbackData);
        }

        try
        {
            await next(ctx);
            if (logger.IsEnabled(LogLevel.Information))
            {
                var elapsedMilliseconds = (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                LogUpdateOut(kind, ctx.UserId, elapsedMilliseconds, failed: false);
            }
        }
        catch
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                var elapsedMilliseconds = (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                LogUpdateOut(kind, ctx.UserId, elapsedMilliseconds, failed: true);
            }
            throw;
        }
    }

    private static string? Truncate(string? s)
    {
        if (s is null || s.Length <= 80)
            return s;

        return s[..80] + "…";
    }

    [LoggerMessage(EventId = 1001, Level = LogLevel.Debug,
        Message = "update.in kind={Kind} user={UserId} text={Text} cb={Cb}")]
    partial void LogUpdateIn(string kind, long userId, string? text, string? cb);

    [LoggerMessage(EventId = 1002, Level = LogLevel.Information,
        Message = "update.out kind={Kind} user={UserId} duration_ms={Ms} failed={Failed}")]
    partial void LogUpdateOut(string kind, long userId, long ms, bool failed);
}
