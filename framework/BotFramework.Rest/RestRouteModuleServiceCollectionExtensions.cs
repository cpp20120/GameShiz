using Microsoft.Extensions.DependencyInjection;

namespace BotFramework.Rest;

public static class RestRouteModuleServiceCollectionExtensions
{
    public static IServiceCollection AddRestRouteModule<TModule>(this IServiceCollection services)
        where TModule : class, IRestRouteModule =>
        services.AddSingleton<IRestRouteModule, TModule>();
}