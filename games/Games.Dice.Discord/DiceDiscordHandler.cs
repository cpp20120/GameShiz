using BotFramework.Discord.Commands;
using BotFramework.Discord.Routing;
using Games.Dice.Contracts.Play;
namespace Games.Dice.Discord;
public sealed class DiceDiscordHandler(IDiceClient client):IDiscordMessageHandler{
public bool CanHandle(DiscordMessageContext c)=>DiscordCommand.Is(c,"slot","slots");
public async Task HandleAsync(DiscordMessageContext c){var value=DiscordCommand.RandomFace(1,64);var result=await client.PlayAsync(new DicePlayRequest(DiscordCommand.UserId(c),DiscordCommand.DisplayName(c),value,DiscordCommand.ScopeId(c),c.Message.Id.ToString(),false),DiscordCommand.Metadata(c),c.CancellationToken);await DiscordCommand.ReplyResultAsync(c,result,"🎰 Слоты: "+value);}}
public static class DiceDiscordModule{public static IServiceCollection AddDiceDiscord(this IServiceCollection s)=>s.AddScoped<IDiscordMessageHandler,DiceDiscordHandler>();}
