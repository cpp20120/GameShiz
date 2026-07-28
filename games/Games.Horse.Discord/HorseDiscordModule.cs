using BotFramework.Discord.Commands;
using BotFramework.Discord.Routing;
using Games.Horse.Application.Services;
using Games.Horse.Domain.Results;

namespace Games.Horse.Discord;
public static class HorseDiscordModule{public static IServiceCollection AddHorseDiscord(this IServiceCollection s)=>s.AddScoped<IDiscordMessageHandler,HorseDiscordHandler>();}
