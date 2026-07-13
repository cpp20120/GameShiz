using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace BotFramework.Discord.Interactions;

public sealed partial class DiscordInteractionRouter(
    IEnumerable<IDiscordInteractionHandler> handlers,
    ILogger<DiscordInteractionRouter> logger)
{
    public async Task RouteAsync(DiscordInteractionContext context)
    {
        foreach (var handler in handlers)
        {
            if (!handler.CanHandle(context.Interaction)) continue;
            await handler.HandleAsync(context);
            return;
        }

        LogUnhandled(logger, context.Interaction.Id, context.Interaction.Type);
        if (!context.Interaction.HasResponded)
            await context.Interaction.RespondAsync("Эта Discord-команда пока не поддерживается.", ephemeral: true);
    }

    [LoggerMessage(LogLevel.Debug, "No Discord interaction handler accepted interaction {InteractionId} ({InteractionType})")]
    private static partial void LogUnhandled(ILogger logger, ulong interactionId, global::Discord.InteractionType interactionType);
}
