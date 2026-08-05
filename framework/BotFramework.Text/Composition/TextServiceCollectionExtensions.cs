using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BotFramework.Text;

public static class TextServiceCollectionExtensions
{
    public static IServiceCollection AddTextProcessing(
        this IServiceCollection services,
        TextNormalizerOptions? normalizerOptions = null,
        MessageEffectExecutorOptions? effectExecutorOptions = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(normalizerOptions ?? new TextNormalizerOptions());
        services.TryAddSingleton(effectExecutorOptions ?? new MessageEffectExecutorOptions());
        services.TryAddSingleton<ITextTokenizer, DefaultTextTokenizer>();
        services.TryAddSingleton<ITextNormalizer, DefaultTextNormalizer>();
        services.TryAddSingleton<IDecisionComposer, DefaultDecisionComposer>();
        services.TryAddScoped<IDecisionEngine, CompositeDecisionEngine>();
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IMessageEffectHandler, IgnoreEffectHandler>());
        services.TryAddScoped<IMessageEffectExecutor, MessageEffectExecutor>();
        services.TryAddScoped<TextPipeline>();
        services.TryAddScoped<ITextProcessingPipeline>(
            provider => provider.GetRequiredService<TextPipeline>());
        return services;
    }

    public static IServiceCollection AddTextTokenizer<TTokenizer>(
        this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
        where TTokenizer : class, ITextTokenizer
    {
        ArgumentNullException.ThrowIfNull(services);
        services.Replace(ServiceDescriptor.Describe(typeof(ITextTokenizer), typeof(TTokenizer), lifetime));
        return services;
    }

    public static IServiceCollection AddTextNormalizer<TNormalizer>(
        this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
        where TNormalizer : class, ITextNormalizer
    {
        ArgumentNullException.ThrowIfNull(services);
        services.Replace(ServiceDescriptor.Describe(typeof(ITextNormalizer), typeof(TNormalizer), lifetime));
        return services;
    }

    public static IServiceCollection AddTextAnalyzer<TAnalyzer>(
        this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where TAnalyzer : class, ITextAnalyzer
    {
        ArgumentNullException.ThrowIfNull(services);
        services.Add(ServiceDescriptor.Describe(typeof(ITextAnalyzer), typeof(TAnalyzer), lifetime));
        return services;
    }

    public static IServiceCollection AddTextPolicy<TPolicy>(
        this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where TPolicy : class, ITextPolicy
    {
        ArgumentNullException.ThrowIfNull(services);
        services.Add(ServiceDescriptor.Describe(typeof(ITextPolicy), typeof(TPolicy), lifetime));
        return services;
    }

    public static IServiceCollection AddTextDecisionComposer<TComposer>(
        this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
        where TComposer : class, IDecisionComposer
    {
        ArgumentNullException.ThrowIfNull(services);
        services.Replace(ServiceDescriptor.Describe(typeof(IDecisionComposer), typeof(TComposer), lifetime));
        return services;
    }

    public static IServiceCollection AddTextDecisionEngine<TDecisionEngine>(
        this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where TDecisionEngine : class, IDecisionEngine
    {
        ArgumentNullException.ThrowIfNull(services);
        services.Replace(ServiceDescriptor.Describe(typeof(IDecisionEngine), typeof(TDecisionEngine), lifetime));
        return services;
    }

    public static IServiceCollection AddTextObserver<TObserver>(
        this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where TObserver : class, IAnalysisObserver
    {
        ArgumentNullException.ThrowIfNull(services);
        services.Add(ServiceDescriptor.Describe(typeof(IAnalysisObserver), typeof(TObserver), lifetime));
        return services;
    }

    public static IServiceCollection AddTextEffectHandler<THandler>(
        this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where THandler : class, IMessageEffectHandler
    {
        ArgumentNullException.ThrowIfNull(services);
        services.Add(ServiceDescriptor.Describe(typeof(IMessageEffectHandler), typeof(THandler), lifetime));
        return services;
    }
}
