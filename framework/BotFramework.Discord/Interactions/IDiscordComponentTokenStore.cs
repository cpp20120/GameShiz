namespace BotFramework.Discord.Interactions;

public interface IDiscordComponentTokenStore
{
    string Issue(string action, string? payload = null);
    bool TryResolve(string customId, out DiscordComponentToken token);
}
