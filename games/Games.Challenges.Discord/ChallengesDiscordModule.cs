using BotFramework.Discord.Commands;
using BotFramework.Discord.Routing;
using Games.Challenges.Application.Services;
using Games.Challenges.Domain.Entities;
using Games.Challenges.Domain.Results;

namespace Games.Challenges.Discord;
public static class ChallengesDiscordModule{public static IServiceCollection AddChallengesDiscord(this IServiceCollection s)=>s.AddScoped<IDiscordMessageHandler,ChallengesDiscordHandler>();}
