using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace BotFramework.Rest;

/// <summary>
/// A transport adapter owns only its typed HTTP routes. It must call an
/// application contract or a resilient client; it must not open a database.
/// </summary>
public interface IRestRouteModule
{
    string ModuleId { get; }

    void Map(IEndpointRouteBuilder endpoints);
}

public static class RestRouteModuleServiceCollectionExtensions
{
    public static IServiceCollection AddRestRouteModule<TModule>(this IServiceCollection services)
        where TModule : class, IRestRouteModule =>
        services.AddSingleton<IRestRouteModule, TModule>();
}
