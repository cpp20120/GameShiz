using Microsoft.Extensions.Logging;

namespace BotFramework.Discord.Routing;

public sealed partial class DiscordMessageRouter(
    IEnumerable<IDiscordMessageHandler> handlers,
    ILogger<DiscordMessageRouter> logger)
{
    public async Task RouteAsync(DiscordMessageContext context)
    {
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
