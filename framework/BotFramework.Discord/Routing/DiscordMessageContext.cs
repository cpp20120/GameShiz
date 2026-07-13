using Discord.WebSocket;

namespace BotFramework.Discord.Routing;

public sealed record DiscordMessageContext(
    SocketMessage Message,
    string CommandText,
    IServiceProvider Services,
    CancellationToken CancellationToken);
