using BotFramework.Rest;
using Games.Poker.Application.Services;
using Games.Poker.Domain.Results;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Games.Poker.Rest;

public static class PokerRestServiceCollectionExtensions
{
    public static IServiceCollection AddPokerRest(this IServiceCollection services) =>
        services.AddRestRouteModule<PokerRestModule>();
}
