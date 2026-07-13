using Discord;
using Discord.WebSocket;

namespace BotFramework.Discord.Interactions;

public interface IDiscordInteractionHandler
{
    IEnumerable<ApplicationCommandProperties> BuildCommands();
    bool CanHandle(SocketInteraction interaction);
    Task HandleAsync(DiscordInteractionContext context);
}
