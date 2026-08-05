using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TextRules.Application.Compilation;
using TextRules.Application.Composition;
using TextRules.Application.Sources;
using TextRules.Infrastructure.Snapshots;
using TextRules.Infrastructure.Sources;

namespace TextRules.Infrastructure.Composition;

public static class TextRulesServiceCollectionExtensions
{
    public static IServiceCollection AddTextRules(
        this IServiceCollection services,
        TextRuleCompilerOptions? compilerOptions = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddTextRulesCore(compilerOptions);
        services.TryAddSingleton<InMemoryTextRuleSource>();
        services.TryAddSingleton<ITextRuleSource>(
            provider => provider.GetRequiredService<InMemoryTextRuleSource>());
        services.TryAddSingleton<ITextRuleSnapshotProvider, CachedTextRuleSnapshotProvider>();
        return services;
    }

    public static IServiceCollection UseTextRuleSource<TSource>(
        this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
        where TSource : class, ITextRuleSource
    {
        ArgumentNullException.ThrowIfNull(services);
        services.Replace(ServiceDescriptor.Describe(typeof(ITextRuleSource), typeof(TSource), lifetime));
        return services;
    }
}
