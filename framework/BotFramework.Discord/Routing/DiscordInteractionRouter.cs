using Microsoft.Extensions.Logging;

namespace BotFramework.Discord.Routing;

public sealed partial class DiscordInteractionRouter(
    IEnumerable<IDiscordInteractionHandler> handlers,
    ILogger<DiscordInteractionRouter> logger)
{
    public async Task RouteAsync(DiscordInteractionContext context)
    {
        foreach (var handler in handlers)
        {
            if (!handler.CanHandle(context)) continue;
            await handler.HandleAsync(context);
            return;
        }

        LogUnhandledInteraction(logger, context.Interaction.Id, context.Interaction.User.Id);
        if (!context.Interaction.HasResponded)
            await context.Interaction.RespondAsync("Unsupported interaction.", ephemeral: true);
    }

    [LoggerMessage(LogLevel.Debug, "No Discord handler accepted interaction {InteractionId} from user {UserId}")]
    private static partial void LogUnhandledInteraction(ILogger logger, ulong interactionId, ulong userId);
}
