using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BotFramework.Text;

public static class TextServiceCollectionExtensions
{
    public static IServiceCollection AddTextProcessing(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<TextNormalizerOptions>();
        services.TryAddSingleton<ITextNormalizer, DefaultTextNormalizer>();
        services.TryAddSingleton<IMessageEffectExecutor, MessageEffectExecutor>();
        services.TryAddSingleton<TextPipeline>();
        return services;
    }

    public static IServiceCollection AddTextAnalyzer<TAnalyzer>(
        this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
        where TAnalyzer : class, ITextAnalyzer
    {
        ArgumentNullException.ThrowIfNull(services);
        services.Add(ServiceDescriptor.Describe(typeof(ITextAnalyzer), typeof(TAnalyzer), lifetime));
        return services;
    }

    public static IServiceCollection AddTextDecisionEngine<TDecisionEngine>(
        this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
        where TDecisionEngine : class, IDecisionEngine
    {
        ArgumentNullException.ThrowIfNull(services);
        services.Add(ServiceDescriptor.Describe(typeof(IDecisionEngine), typeof(TDecisionEngine), lifetime));
        return services;
    }

    public static IServiceCollection AddTextObserver<TObserver>(
        this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
        where TObserver : class, IAnalysisObserver
    {
        ArgumentNullException.ThrowIfNull(services);
        services.Add(ServiceDescriptor.Describe(typeof(IAnalysisObserver), typeof(TObserver), lifetime));
        return services;
    }

    public static IServiceCollection AddTextEffectHandler<THandler>(
        this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
        where THandler : class, IMessageEffectHandler
    {
        ArgumentNullException.ThrowIfNull(services);
        services.Add(ServiceDescriptor.Describe(typeof(IMessageEffectHandler), typeof(THandler), lifetime));
        return services;
    }
}
