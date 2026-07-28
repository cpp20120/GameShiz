using BotFramework.Discord.Commands;
using BotFramework.Discord.Routing;
using Games.Dice.Contracts.Play;
namespace Games.Dice.Discord;
public sealed class DiceDiscordHandler(IDiceClient client):IDiscordMessageHandler{
public bool CanHandle(DiscordMessageContext context)=>DiscordCommand.Is(context,"slot","slots");
public async Task HandleAsync(DiscordMessageContext context){var value=DiscordCommand.RandomFace(1,64);var result=await client.PlayAsync(new DicePlayRequest(DiscordCommand.UserId(context),DiscordCommand.DisplayName(context),value,DiscordCommand.ScopeId(context),context.Message.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),false),DiscordCommand.Metadata(context),context.CancellationToken);await DiscordCommand.ReplyResultAsync(context,result,"🎰 Слоты: "+value);}}
