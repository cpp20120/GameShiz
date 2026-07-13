using Discord.WebSocket;

namespace BotFramework.Discord.Routing;

public sealed record DiscordInteractionContext(
    SocketInteraction Interaction,
    IServiceProvider Services,
    CancellationToken CancellationToken);

public interface IDiscordInteractionHandler
{
    bool CanHandle(DiscordInteractionContext context);
    Task HandleAsync(DiscordInteractionContext context);
}
