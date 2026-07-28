using BotFramework.Discord.Commands;
using BotFramework.Discord.Routing;
using Games.Dice.Contracts.Play;
namespace Games.Dice.Discord;
public static class DiceDiscordModule{public static IServiceCollection AddDiceDiscord(this IServiceCollection s)=>s.AddScoped<IDiscordMessageHandler,DiceDiscordHandler>();}
