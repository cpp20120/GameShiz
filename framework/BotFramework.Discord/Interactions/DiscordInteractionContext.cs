using Discord.WebSocket;

namespace BotFramework.Discord.Interactions;

public sealed record DiscordInteractionContext(
    SocketInteraction Interaction,
    IServiceProvider Services,
    CancellationToken CancellationToken,
    string CultureCode = "ru");
