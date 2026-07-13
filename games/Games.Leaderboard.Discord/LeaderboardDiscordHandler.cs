using BotFramework.Discord.Commands;
using BotFramework.Discord.Routing;
using Games.Leaderboard.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace Games.Leaderboard.Discord;

public sealed class LeaderboardDiscordHandler(ILeaderboardClient client) : IDiscordMessageHandler
{
    public bool CanHandle(DiscordMessageContext context) => DiscordCommand.TryParse(context, out var c) && c.Is("balance", "top", "daily");
    public async Task HandleAsync(DiscordMessageContext context)
    {
        if (!DiscordCommand.TryParse(context, out var command)) return;
        var uid = context.UserId(); var scope = context.ScopeId();
        if (command.Is("balance"))
        {
            var b = await client.GetBalanceAsync(uid, scope, context.DisplayName(), context.CancellationToken);
            await context.ReplyAsync(b.Visible ? $"Balance: **{b.Coins}**" : "Balance is hidden."); return;
        }
        if (command.Is("daily"))
        {
            var r = await client.ClaimDailyAsync(uid, scope, context.DisplayName(), context.CancellationToken);
            await context.ReplyAsync(r.Status == DailyClaimStatus.Claimed ? $"Daily bonus: **+{r.BonusCoins}**. Balance: **{r.NewBalance}**" : $"Daily bonus: **{r.Status}**"); return;
        }
        var top = await client.GetTopAsync(10, scope, context.CancellationToken);
        var lines = top.Places.SelectMany(p => p.Users.Select(u => $"**{p.Place}.** {u.DisplayName} — **{u.Coins}**"));
        await context.ReplyAsync("🏆 Leaderboard\n" + (lines.Any() ? string.Join('\n', lines) : "No players yet."));
    }
}
public static class LeaderboardDiscordExtensions { public static IServiceCollection AddLeaderboardDiscord(this IServiceCollection s) => s.AddScoped<IDiscordMessageHandler, LeaderboardDiscordHandler>(); }
