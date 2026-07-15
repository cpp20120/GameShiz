using Microsoft.Extensions.Logging;
using BotFramework.Discord.Commands;
using BotFramework.Discord.Hosting;

namespace BotFramework.Discord.Routing;

public sealed partial class DiscordMessageRouter(
    IEnumerable<IDiscordMessageHandler> handlers,
    ILogger<DiscordMessageRouter> logger,
    DiscordUxRateLimiter rateLimiter)
{
    public async Task RouteAsync(DiscordMessageContext context)
    {
        var commandName = context.CommandText.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "unknown";
        var decision = rateLimiter.Check(context.Message.Author.Id, $"command:{commandName}", interaction: false);
        if (!decision.Allowed)
        {
            var seconds = Math.Max(1, (int)Math.Ceiling(decision.RetryAfter.TotalSeconds));
            await DiscordCommand.ReplyAsync(
                context,
                DiscordLocalization.Format("rate.limited", context.CultureCode, seconds),
                isError: true);
            return;
        }

        foreach (var handler in handlers)
        {
            if (!handler.CanHandle(context)) continue;
            await handler.HandleAsync(context);
            return;
        }

        LogUnhandledMessage(logger, context.Message.Id, context.Message.Author.Id);
    }

    [LoggerMessage(LogLevel.Debug, "No Discord handler accepted message {MessageId} from user {UserId}")]
    private static partial void LogUnhandledMessage(ILogger logger, ulong messageId, ulong userId);
}
