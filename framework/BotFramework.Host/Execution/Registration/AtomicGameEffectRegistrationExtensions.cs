using Microsoft.Extensions.DependencyInjection;

namespace BotFramework.Host.Execution;

public static class AtomicGameEffectRegistrationExtensions
{
    public static IServiceCollection AddAtomicGameEffectHandler<THandler>(this IServiceCollection services)
        where THandler : class, IGameEffectHandler
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddScoped<IGameEffectHandler, THandler>();
        return services;
    }
}
