using Discord.WebSocket;
using BotFramework.Discord.Hosting;
using Microsoft.Extensions.Logging;

namespace BotFramework.Discord.Interactions;

public sealed partial class DiscordInteractionRouter(
    IEnumerable<IDiscordInteractionHandler> handlers,
    ILogger<DiscordInteractionRouter> logger,
    IDiscordComponentTokenStore tokenStore,
    DiscordUxRateLimiter rateLimiter)
{
    public async Task RouteAsync(DiscordInteractionContext context)
    {
        if (context.Interaction is SocketMessageComponent or SocketModal)
        {
            var customId = context.Interaction switch
            {
                SocketMessageComponent component => component.Data.CustomId,
                SocketModal modal => modal.Data.CustomId,
                _ => string.Empty,
            };
            if (!tokenStore.TryResolve(customId, out _))
            {
                await DiscordInteraction.ReplyAsync(
                    context,
                    DiscordLocalization.Get("component.stale", context.CultureCode),
                    ephemeral: true);
                return;
            }
        }

        var isAutocomplete = context.Interaction is SocketAutocompleteInteraction;
        var decision = rateLimiter.Check(
            context.Interaction.User.Id,
            BucketName(context.Interaction, tokenStore),
            isAutocomplete);
        if (!decision.Allowed)
        {
            var seconds = Math.Max(1, (int)Math.Ceiling(decision.RetryAfter.TotalSeconds));
            await DiscordInteraction.ReplyAsync(
                context,
                DiscordLocalization.Format("rate.limited", context.CultureCode, seconds),
                ephemeral: true);
            return;
        }

        foreach (var handler in handlers)
        {
            if (!handler.CanHandle(context.Interaction)) continue;
            await handler.HandleAsync(context);
            return;
        }

        LogUnhandled(logger, context.Interaction.Id, context.Interaction.Type);
        if (!context.Interaction.HasResponded)
            await DiscordInteraction.ReplyAsync(
                context,
                DiscordLocalization.Get("interaction.unhandled", context.CultureCode),
                ephemeral: true);
    }

    private static string BucketName(SocketInteraction interaction, IDiscordComponentTokenStore tokenStore) => interaction switch
    {
        SocketSlashCommand command => $"slash:{command.Data.Name}",
        SocketAutocompleteInteraction autocomplete => $"autocomplete:{autocomplete.Data.CommandName}",
        SocketMessageComponent component when tokenStore.TryResolve(component.Data.CustomId, out var token) => $"component:{token.Action}",
        SocketModal modal when tokenStore.TryResolve(modal.Data.CustomId, out var modalToken) => $"modal:{modalToken.Action}",
        _ => interaction.Type.ToString(),
    };

    [LoggerMessage(LogLevel.Debug, "No Discord interaction handler accepted interaction {InteractionId} ({InteractionType})")]
    private static partial void LogUnhandled(ILogger logger, ulong interactionId, global::Discord.InteractionType interactionType);
}
