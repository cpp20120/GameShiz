using BotFramework.Rest;
using Games.Leaderboard.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Games.Leaderboard.Rest;

public static class LeaderboardRestServiceCollectionExtensions
{
    public static IServiceCollection AddLeaderboardRest(this IServiceCollection services) =>
        services.AddRestRouteModule<LeaderboardRestModule>();
}
