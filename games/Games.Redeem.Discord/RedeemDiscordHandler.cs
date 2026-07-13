using BotFramework.Discord.Commands;
using BotFramework.Discord.Routing;
using Discord;
using Discord.WebSocket;
using Games.Redeem.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace Games.Redeem.Discord;

public sealed class RedeemDiscordHandler(IRedeemClient client) : IDiscordMessageHandler, IDiscordInteractionHandler
{
    public bool CanHandle(DiscordMessageContext context) => DiscordCommand.TryParse(context, out var c) && c.Is("redeem");
    public async Task HandleAsync(DiscordMessageContext context)
    {
        if (!DiscordCommand.TryParse(context, out var c) || c.Arguments.Count != 1) { await context.ReplyAsync("Usage: `redeem <code>`"); return; }
        var r=await client.BeginAsync(context.UserId(),context.ScopeId(),context.DisplayName(),c.Arguments[0],context.CancellationToken);
        if(r.Error!=RedeemClientError.None||r.Captcha is null){await context.ReplyAsync($"Redeem: **{r.Error}**");return;}
        var b=new ComponentBuilder(); foreach(var item in r.Captcha.Items.Take(5)) b.WithButton(item.Text,$"redeem:{r.CodeGuid:N}:{item.Data}",ButtonStyle.Secondary);
        await context.Message.Channel.SendMessageAsync($"Captcha: **{r.Captcha.Pattern}**",components:b.Build());
    }
    public bool CanHandle(DiscordInteractionContext context)=>context.Interaction is SocketMessageComponent c&&c.Data.CustomId.StartsWith("redeem:",StringComparison.Ordinal);
    public async Task HandleAsync(DiscordInteractionContext context)
    {
        var c=(SocketMessageComponent)context.Interaction; var parts=c.Data.CustomId.Split(':');
        if(parts.Length!=3||!Guid.TryParseExact(parts[1],"N",out var id)||!int.TryParse(parts[2],out var choice)){await c.RespondAsync("Invalid captcha.",ephemeral:true);return;}
        var uid=checked((long)c.User.Id); if(!await client.VerifyCaptchaAsync(uid,id,choice,context.CancellationToken)){await c.RespondAsync("Wrong captcha.",ephemeral:true);return;}
        var r=await client.CompleteAsync(uid,checked((long)c.ChannelId!.Value),id,context.CancellationToken);
        await c.UpdateAsync(p=>{p.Content=r.Error==RedeemClientError.None?"Code redeemed successfully.":$"Redeem: **{r.Error}**";p.Components=null;});
    }
}
public static class RedeemDiscordExtensions { public static IServiceCollection AddRedeemDiscord(this IServiceCollection s)=>s.AddScoped<IDiscordMessageHandler,RedeemDiscordHandler>().AddScoped<IDiscordInteractionHandler,RedeemDiscordHandler>(); }
