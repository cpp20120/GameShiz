using BotFramework.Discord.Commands;
using BotFramework.Discord.Routing;
using Games.Horse.Application.Services;
using Games.Horse.Domain.Results;

namespace Games.Horse.Discord;
public sealed class HorseDiscordHandler(IHorseService service) : IDiscordMessageHandler
{
    public bool CanHandle(DiscordMessageContext c)=>DiscordCommand.Is(c,"horse");
    public async Task HandleAsync(DiscordMessageContext c)
    {
        var p=DiscordCommand.Parts(c); if(p.Length<2){await Usage(c);return;} object result;
        switch(p[1].ToLowerInvariant())
        {
            case "bet" when p.Length>=4 && int.TryParse(p[2],out var horse) && int.TryParse(p[3],out var amount) && amount>0:
                result=await service.PlaceBetAsync(DiscordCommand.UserId(c),DiscordCommand.DisplayName(c),DiscordCommand.ScopeId(c),horse,amount,DiscordCommand.SourceId(c),c.CancellationToken);break;
            case "info": result=await service.GetTodayInfoAsync(DiscordCommand.ScopeId(c),c.CancellationToken);break;
            case "result": result=await service.GetTodayResultAsync(DiscordCommand.ScopeId(c),c.CancellationToken);break;
            case "run": result=await service.RunRaceAsync(DiscordCommand.UserId(c),HorseRunKind.Local,DiscordCommand.ScopeId(c),c.CancellationToken);break;
            default: await Usage(c);return;
        }
        await DiscordCommand.ReplyResultAsync(c,result,"Horse");
    }
    private static Task Usage(DiscordMessageContext c)=>DiscordCommand.ReplyAsync(c,"`horse bet <1..6> <amount>` | `horse info|result|run`");
}
public static class HorseDiscordModule{public static IServiceCollection AddHorseDiscord(this IServiceCollection s)=>s.AddScoped<IDiscordMessageHandler,HorseDiscordHandler>();}
