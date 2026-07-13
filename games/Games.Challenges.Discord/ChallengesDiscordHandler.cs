using BotFramework.Discord.Commands;
using BotFramework.Discord.Routing;
using Games.Challenges.Application.Services;
using Games.Challenges.Domain.Entities;
using Games.Challenges.Domain.Results;

namespace Games.Challenges.Discord;
public sealed class ChallengesDiscordHandler(IChallengeService service):IDiscordMessageHandler
{
    public bool CanHandle(DiscordMessageContext c)=>DiscordCommand.Is(c,"challenge","ch");
    public async Task HandleAsync(DiscordMessageContext c)
    {
        var p=DiscordCommand.Parts(c);var uid=DiscordCommand.UserId(c);object result;
        if(p.Length>=4 && p[1].Equals("create",StringComparison.OrdinalIgnoreCase))
        {
            var target=c.Message.MentionedUsers.FirstOrDefault();
            if(target is null||!int.TryParse(p[^2],out var amount)||!Enum.TryParse<ChallengeGame>(p[^1],true,out var game)){await Usage(c);return;}
            result=await service.CreateAsync(uid,DiscordCommand.DisplayName(c),new ChallengeUser(checked((long)target.Id),target.GlobalName??target.Username),DiscordCommand.ScopeId(c),amount,game,c.CancellationToken);
        }
        else if(p.Length>=3 && p[1].Equals("accept",StringComparison.OrdinalIgnoreCase) && Guid.TryParse(p[2],out var acceptId))
        {
            var begun=await service.BeginAcceptAsync(acceptId,uid,c.CancellationToken);
            if(begun.Error!=ChallengeAcceptError.None||begun.Challenge is null){result=begun;}
            else
            {
                var max=begun.Challenge.Game switch{ChallengeGame.Football or ChallengeGame.Basketball=>5,ChallengeGame.Slots=>64,_=>6};
                var first=DiscordCommand.RandomFace(1,max);var second=DiscordCommand.RandomFace(1,max);
                result=await service.CompleteAcceptedAsync(begun.Challenge,first,second,c.CancellationToken);
            }
        }
        else if(p.Length>=3 && p[1].Equals("decline",StringComparison.OrdinalIgnoreCase) && Guid.TryParse(p[2],out var declineId)) result=await service.DeclineAsync(declineId,uid,c.CancellationToken);
        else {await Usage(c);return;}
        await DiscordCommand.ReplyResultAsync(c,result,"Challenge");
    }
    private static Task Usage(DiscordMessageContext c)=>DiscordCommand.ReplyAsync(c,"`challenge create @user <amount> <Dice|DiceCube|Darts|Bowling|Basketball|Football|Slots|Horse|Blackjack>`\n`challenge accept <guid>` | `challenge decline <guid>`");
}
public static class ChallengesDiscordModule{public static IServiceCollection AddChallengesDiscord(this IServiceCollection s)=>s.AddScoped<IDiscordMessageHandler,ChallengesDiscordHandler>();}
