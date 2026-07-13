using BotFramework.Discord.Routing;

namespace BotFramework.Discord.Commands;

public sealed class DiscordHelpHandler : IDiscordMessageHandler
{
    public bool CanHandle(DiscordMessageContext context) => DiscordCommand.Is(context, "help", "games");

    public Task HandleAsync(DiscordMessageContext context) => DiscordCommand.ReplyAsync(context,
        "**CasinoShiz Discord commands**\n" +
        "`slot` — slots\n" +
        "`dice <bet>` — cube\n" +
        "`darts <bet>` — darts\n" +
        "`football <bet>` — football\n" +
        "`basketball <bet>` — basketball\n" +
        "`bowling <bet>` — bowling\n" +
        "`balance`, `top`, `globaltop`, `daily`\n" +
        "`transfer @user <amount>`\n" +
        "`blackjack start <bet>` / `blackjack hit|stand|double|state`");
}
