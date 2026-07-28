using BotFramework.Rest;
using Games.Blackjack.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Games.Blackjack.Rest;

public static class BlackjackRestServiceCollectionExtensions
{
    public static IServiceCollection AddBlackjackRest(this IServiceCollection services) => services.AddRestRouteModule<BlackjackRestModule>();
}
