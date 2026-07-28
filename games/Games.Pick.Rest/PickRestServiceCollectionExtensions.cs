using BotFramework.Rest;
using Games.Pick.Application.Services;
using Games.Pick.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Games.Pick.Rest;

public static class PickRestServiceCollectionExtensions
{
    public static IServiceCollection AddPickRest(this IServiceCollection services) => services.AddRestRouteModule<PickRestModule>();
}
