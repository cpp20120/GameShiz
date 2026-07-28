using BotFramework.Rest;
using Games.PixelBattle.Contracts;
using Games.PixelBattle.Domain.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Games.PixelBattle.Rest;

public static class PixelBattleRestServiceCollectionExtensions
{
    public static IServiceCollection AddPixelBattleRest(this IServiceCollection services) => services.AddRestRouteModule<PixelBattleRestModule>();
}
