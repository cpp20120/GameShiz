using BotFramework.Discord.Commands;
using BotFramework.Discord.Routing;
using Games.Pick.Application.Services;

namespace Games.Pick.Discord;
public sealed class PickDiscordHandler(IPickClient client) : IDiscordMessageHandler
{
    public bool CanHandle(DiscordMessageContext c)=>DiscordCommand.Is(c,"pick");
    public async Task HandleAsync(DiscordMessageContext c)
    {
        var p=DiscordCommand.Parts(c); if(p.Length<2){await Usage(c);return;} var uid=DiscordCommand.UserId(c);var scope=DiscordCommand.ScopeId(c);var name=DiscordCommand.DisplayName(c);object? r;
        switch(p[1].ToLowerInvariant())
        {
            case "choose" when p.Length>=5 && int.TryParse(p[2],out var amount):
                var variants=string.Join(' ',p.Skip(4)).Split('|',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries);
                var backed=p[3].Split(',',StringSplitOptions.RemoveEmptyEntries).Select(x=>int.TryParse(x,out var i)?i:-1).Where(i=>i>=0).ToArray();
                r=await client.PickAsync(uid,name,scope,amount,variants,backed,DiscordCommand.SourceId(c),c.CancellationToken);break;
            case "lottery" when p.Length>=4 && p[2].Equals("open",StringComparison.OrdinalIgnoreCase) && int.TryParse(p[3],out var stake): r=await client.OpenLotteryAsync(uid,name,scope,stake,DiscordCommand.SourceId(c),c.CancellationToken);break;
            case "lottery" when p.Length>=3 && p[2].Equals("join",StringComparison.OrdinalIgnoreCase): r=await client.JoinLotteryAsync(uid,name,scope,DiscordCommand.SourceId(c),c.CancellationToken);break;
            case "lottery" when p.Length>=3 && p[2].Equals("info",StringComparison.OrdinalIgnoreCase): r=await client.LotteryInfoAsync(scope,c.CancellationToken);break;
            case "lottery" when p.Length>=3 && p[2].Equals("cancel",StringComparison.OrdinalIgnoreCase): r=await client.CancelLotteryAsync(uid,scope,c.CancellationToken);break;
            case "daily" when p.Length>=4 && p[2].Equals("buy",StringComparison.OrdinalIgnoreCase) && int.TryParse(p[3],out var count): r=await client.BuyDailyAsync(uid,name,scope,count,DiscordCommand.SourceId(c),c.CancellationToken);break;
            case "daily" when p.Length>=3 && p[2].Equals("info",StringComparison.OrdinalIgnoreCase): r=await client.DailyInfoAsync(scope,uid,c.CancellationToken);break;
            case "daily" when p.Length>=3 && p[2].Equals("history",StringComparison.OrdinalIgnoreCase): r=await client.DailyHistoryAsync(scope,10,c.CancellationToken);break;
            default: await Usage(c);return;
        }
        await DiscordCommand.ReplyResultAsync(c,r,"Pick");
    }
    private static Task Usage(DiscordMessageContext c)=>DiscordCommand.ReplyAsync(c,"`pick choose <amount> <backed indexes, e.g. 0,2> <a|b|c>`\n`pick lottery open <stake>|join|info|cancel`\n`pick daily buy <count>|info|history`");
}
public static class PickDiscordModule{public static IServiceCollection AddPickDiscord(this IServiceCollection s)=>s.AddScoped<IDiscordMessageHandler,PickDiscordHandler>();}
