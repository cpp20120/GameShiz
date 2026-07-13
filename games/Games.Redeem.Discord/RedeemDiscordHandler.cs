using System.Collections.Concurrent;
using BotFramework.Discord.Commands;
using BotFramework.Discord.Routing;
using Games.Redeem.Contracts;

namespace Games.Redeem.Discord;
public sealed class RedeemDiscordHandler(IRedeemClient client) : IDiscordMessageHandler
{
    private static readonly ConcurrentDictionary<long, Guid> Pending = new();
    public bool CanHandle(DiscordMessageContext c)=>DiscordCommand.Is(c,"redeem");
    public async Task HandleAsync(DiscordMessageContext c)
    {
        var p=DiscordCommand.Parts(c);var uid=DiscordCommand.UserId(c);var scope=DiscordCommand.ScopeId(c);
        if(p.Length>=3 && p[1].Equals("captcha",StringComparison.OrdinalIgnoreCase) && int.TryParse(p[2],out var choice) && Pending.TryGetValue(uid,out var codeGuid))
        {
            if(!await client.VerifyCaptchaAsync(uid,codeGuid,choice,c.CancellationToken)){await DiscordCommand.ReplyAsync(c,"Неверная капча.");return;}
            var done=await client.CompleteAsync(uid,scope,codeGuid,c.CancellationToken);Pending.TryRemove(uid,out _);await DiscordCommand.ReplyResultAsync(c,done,"Redeem");return;
        }
        if(p.Length!=2){await DiscordCommand.ReplyAsync(c,"`redeem <code>` затем `redeem captcha <id>`");return;}
        var begun=await client.BeginAsync(uid,scope,DiscordCommand.DisplayName(c),p[1],c.CancellationToken);
        if(begun.Error!=RedeemClientError.None){await DiscordCommand.ReplyResultAsync(c,begun,"Redeem");return;}
        if(begun.Captcha is null){var done=await client.CompleteAsync(uid,scope,begun.CodeGuid,c.CancellationToken);await DiscordCommand.ReplyResultAsync(c,done,"Redeem");return;}
        Pending[uid]=begun.CodeGuid;
        var options=string.Join("\n",begun.Captcha.Items.Select(x=>$"`{x.Data}` — {x.Text}"));
        await DiscordCommand.ReplyAsync(c,$"**{begun.Captcha.Pattern}**\n{options}\nОтвет: `redeem captcha <id>`");
    }
}
public static class RedeemDiscordModule{public static IServiceCollection AddRedeemDiscord(this IServiceCollection s)=>s.AddScoped<IDiscordMessageHandler,RedeemDiscordHandler>();}
