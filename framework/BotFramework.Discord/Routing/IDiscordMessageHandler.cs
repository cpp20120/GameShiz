namespace BotFramework.Discord.Routing;

public interface IDiscordMessageHandler
{
    bool CanHandle(DiscordMessageContext context);
    Task HandleAsync(DiscordMessageContext context);
}
