using BotFramework.Discord.Commands;
using BotFramework.Discord.Interactions;
using BotFramework.Discord.Routing;
using Discord;
using Discord.WebSocket;
using Games.Leaderboard.Contracts;

namespace Games.Leaderboard.Discord;

public sealed class LeaderboardDiscordHandler(ILeaderboardClient client) : IDiscordMessageHandler
{
    public bool CanHandle(DiscordMessageContext context) => DiscordCommand.Is(context, "balance", "top", "daily", "globaltop");
    public async Task HandleAsync(DiscordMessageContext context)
    {
        var p = DiscordCommand.Parts(context);
        var cmd = p[0].ToLowerInvariant();
        var uid = DiscordCommand.UserId(context);
        var scope = DiscordCommand.ScopeId(context);
        var name = DiscordCommand.DisplayName(context);
        object r = cmd switch
        {
            "balance" => await client.GetBalanceAsync(uid, scope, name, context.CancellationToken),
            "daily" => await client.ClaimDailyAsync(uid, scope, name, context.CancellationToken),
            "globaltop" => await client.GetGlobalTopAsync(10, context.CancellationToken),
            _ => await client.GetTopAsync(10, scope, context.CancellationToken),
        };
        await DiscordCommand.ReplyResultAsync(context, r, "Leaderboard");
    }
}
