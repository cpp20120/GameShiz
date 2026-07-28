using BotFramework.Discord.Commands;
using BotFramework.Discord.Routing;
using Games.Pick.Application.Services;

namespace Games.Pick.Discord;
public sealed class PickDiscordHandler(IPickClient client) : IDiscordMessageHandler
{
    public bool CanHandle(DiscordMessageContext context)=>DiscordCommand.Is(context,"pick");
    public async Task HandleAsync(DiscordMessageContext context)
    {
        var p=DiscordCommand.Parts(context); if(p.Length<2){await Usage(context);return;} var uid=DiscordCommand.UserId(context);var scope=DiscordCommand.ScopeId(context);var name=DiscordCommand.DisplayName(context);object? r;
        switch(p[1].ToLowerInvariant())
        {
            case "choose" when p.Length>=5 && int.TryParse(p[2], System.Globalization.CultureInfo.InvariantCulture, out var amount):
                var variants=string.Join(' ',p.Skip(4)).Split('|',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries);
                var backed=p[3].Split(',',StringSplitOptions.RemoveEmptyEntries).Select(x=>int.TryParse(x, System.Globalization.CultureInfo.InvariantCulture, out var i)?i:-1).Where(i=>i>=0).ToArray();
                r=await client.PickAsync(uid,name,scope,amount,variants,backed,DiscordCommand.SourceId(context),context.CancellationToken);break;
            case "lottery" when p.Length>=4 && p[2].Equals("open",StringComparison.OrdinalIgnoreCase) && int.TryParse(p[3], System.Globalization.CultureInfo.InvariantCulture, out var stake): r=await client.OpenLotteryAsync(uid,name,scope,stake,DiscordCommand.SourceId(context),context.CancellationToken);break;
            case "lottery" when p.Length>=3 && p[2].Equals("join",StringComparison.OrdinalIgnoreCase): r=await client.JoinLotteryAsync(uid,name,scope,DiscordCommand.SourceId(context),context.CancellationToken);break;
            case "lottery" when p.Length>=3 && p[2].Equals("info",StringComparison.OrdinalIgnoreCase): r=await client.LotteryInfoAsync(scope,context.CancellationToken);break;
            case "lottery" when p.Length>=3 && p[2].Equals("cancel",StringComparison.OrdinalIgnoreCase): r=await client.CancelLotteryAsync(uid,scope,context.CancellationToken);break;
            case "daily" when p.Length>=4 && p[2].Equals("buy",StringComparison.OrdinalIgnoreCase) && int.TryParse(p[3], System.Globalization.CultureInfo.InvariantCulture, out var count): r=await client.BuyDailyAsync(uid,name,scope,count,DiscordCommand.SourceId(context),context.CancellationToken);break;
            case "daily" when p.Length>=3 && p[2].Equals("info",StringComparison.OrdinalIgnoreCase): r=await client.DailyInfoAsync(scope,uid,context.CancellationToken);break;
            case "daily" when p.Length>=3 && p[2].Equals("history",StringComparison.OrdinalIgnoreCase): r=await client.DailyHistoryAsync(scope,10,context.CancellationToken);break;
            default: await Usage(context);return;
        }
        await DiscordCommand.ReplyResultAsync(context,r,"Pick");
    }
    private static Task Usage(DiscordMessageContext context)=>DiscordCommand.ReplyAsync(context,"`pick choose <amount> <backed indexes, e.g. 0,2> <a|b|context>`\n`pick lottery open <stake>|join|info|cancel`\n`pick daily buy <count>|info|history`");
}
