using BotFramework.Discord.Commands;
using BotFramework.Discord.Routing;
using Games.Horse.Application.Services;
using Games.Horse.Domain.Results;

namespace Games.Horse.Discord;
public sealed class HorseDiscordHandler(IHorseService service) : IDiscordMessageHandler
{
    public bool CanHandle(DiscordMessageContext context)=>DiscordCommand.Is(context,"horse");
    public async Task HandleAsync(DiscordMessageContext context)
    {
        var p=DiscordCommand.Parts(context); if(p.Length<2){await Usage(context);return;} object result;
        switch(p[1].ToLowerInvariant())
        {
            case "bet" when p.Length>=4 && int.TryParse(p[2], System.Globalization.CultureInfo.InvariantCulture, out var horse) && int.TryParse(p[3], System.Globalization.CultureInfo.InvariantCulture, out var amount) && amount>0:
                result=await service.PlaceBetAsync(DiscordCommand.UserId(context),DiscordCommand.DisplayName(context),DiscordCommand.ScopeId(context),horse,amount,DiscordCommand.SourceId(context),context.CancellationToken);break;
            case "info": result=await service.GetTodayInfoAsync(DiscordCommand.ScopeId(context),context.CancellationToken);break;
            case "result": result=await service.GetTodayResultAsync(DiscordCommand.ScopeId(context),context.CancellationToken);break;
            case "run": result=await service.RunRaceAsync(DiscordCommand.UserId(context),HorseRunKind.ThisChat,DiscordCommand.ScopeId(context),context.CancellationToken);break;
            default: await Usage(context);return;
        }
        await DiscordCommand.ReplyResultAsync(context,result,"Horse");
    }
    private static Task Usage(DiscordMessageContext context)=>DiscordCommand.ReplyAsync(context,"`horse bet <1..6> <amount>` | `horse info|result|run`");
}
